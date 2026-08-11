# Data grid hardening

Not a Slate stage. Stage 11 built both grids, the column model and the look, and all of
that holds. This is what comes after: the defects found reading that code back, and the
behaviour a data tool leans on that the stage never set out to build.

Read `stage-11-data-grid.md` first for what exists and why, and Grids in
`.claude/skills/kitbash-surfaces` for the rules already written down. Nothing here
reopens a decision made there. The two grids stay ours, the column model stays one, a
width stays pixel or star, and the chrome stays on the root tone.

## Why this exists

Every tool this app will host is a data entry and management tool. The grid is the
surface a person spends the day in, so it is the one control where a rough edge is paid
for a thousand times. Stage 11 asked for a grid that reads cleanly at 31px, sorts, edits
in place and groups, and it delivered that. It did not ask for a current cell, a keyboard
contract, a clipboard or a row model that survives a sort, because nothing had needed
them yet.

## What this was built from

The library was read in full, twenty one files under `Kitbash.Ui/Controls` plus
`Themes/Controls/Grid.axaml`, along with the three launcher consumers and the one test.
Outside it: the W3C grid pattern, which is the only real specification for the keyboard,
AG Grid for the clipboard and range behaviour, the WPF DataGrid and the Windows Community
Toolkit DataGrid for the editing transaction, Avalonia's own TreeDataGrid for what the
platform already has words for, and the enterprise data table UX writing for the parts
that are conventions rather than contracts.

**Everything in the defects section is read off the code and not measured.** Two are
marked as needing proof before anything is changed for them. Write the test first in
both cases, since a fix for a defect that is not there is worse than the defect.

## The rule under all of it

**Both grids say the same thing.** Sorting, editing, the counts and any future current
cell live on `DataGrid` today and `TreeDataGrid` has none of them. That is exactly how
defect 5 below happened, and every feature in this plan will happen the same way unless
the shared piece owns it. `GridFrame` is already the shared chrome and `IGridRowLayout`
is already the shared row seam. Grow those rather than writing a second copy.

## Defects

**All ten are fixed**, eight in phase 1 and the rest in phase 2. Each was proved with a
failing headless test first, and seven were proved a second way by putting the old code
back and watching the new test go red.

| # | What | Where | State |
|---|---|---|---|
| 1 | Selection hands back the wrapper, not the data | `DataGrid.cs`, three launcher views | fixed, was live |
| 2 | Every rebuild destroys row identity | `GridRows.cs` | fixed, was live |
| 3 | The source is never observed | `GridRows.cs` | fixed, was live |
| 4 | A plain list draws blank rows in silence | `DataGrid.cs:203` | fixed |
| 5 | Header sorting is dead in the tree grid | `DataGridHeaderCell.cs:59` | fixed, was live |
| 6 | Focus leaving the window kills the edit | `DataGridCell.cs:105` | fixed, was live and wider |
| 7 | An edit survives its own row being recycled | `DataGrid.cs:242` | fixed, was live |
| 8 | Group headings are selectable from the keyboard | `GridGroupRow.cs:88` | fixed |
| 9 | Sorting is fragile in four ways | `GridRows.cs` | fixed |
| 10 | Attaching a pager turns paging on behind the caller | `GridPager.cs:119` | fixed |

**Defect 2 was the worst of them.** Sorting a column emptied the selection outright, so a
person picking twenty rows and then sorting to check them lost all twenty.

### 1. Selection hands back the wrapper, not the data

`DataGrid` is a `ListBox` over `GridRow`, so `SelectedItem` and `SelectedItems` are
`GridRow` objects rather than the things a caller put in. All three launcher editors bind
`SelectedItem="{Binding Selected}"` where `Selected` is the row view model, in
`CustomToolsEditorView.axaml:15`, `ToolRepositoriesEditorView.axaml:14` and
`WorkspaceLinksEditorView.axaml:14`. A `GridRow` does not convert to a
`CustomToolRowViewModel`, so the binding never lands, `CanRemove` never goes true and
Remove is dead. The write in the other direction is worse: `Add` sets `Selected` to the
new row, the grid cannot find that item among its wrappers, and it resets the selection
to nothing.

**Prove it first.** A headless test over `SettingsListEditor` that adds a row, asserts
`Selected` is that row, and asserts `CanRemove`.

**What it should do.** The grid publishes the data, not the wrapper. `SelectedData` and
`SelectedDataItems` beside the inherited pair, and the settings editors bind to those.
Keeping `SelectedItem` meaning the wrapper is fine as long as nothing is expected to bind
to it.

### 2. Every rebuild destroys row identity

`GridRows.Build` allocates a fresh `GridRow` per item and replaces the whole list with
`rows.Clear()` then `rows.AddRange(paged)`. So sorting a column, toggling a group,
turning a page or calling `Refresh` raises a collection reset. Selection goes, keyboard
focus goes, and the scroll jumps to the top. In a tool where a person picks twenty rows
and then sorts to check them, that is the whole workflow.

**What it should do.** One wrapper per item, kept in a map and reused, so a row that was
picked before a sort is the same object after it. The rebuild diffs into the existing
list rather than clearing it. Selection and the current cell are restored by item.

### 3. The source is never observed

`GridRows` takes an `IEnumerable`, snapshots it with `Cast` and `ToList`, and never
subscribes to `INotifyCollectionChanged`. Every consumer has to call `Refresh` by hand,
which `SettingsListEditor.Announce` does, and each call is a full re-sort, re-group,
re-page and a full reset, which is defect 2 again.

**What it should do.** Subscribe when the source notifies. An insert finds its place
under the sort in force rather than rebuilding, and a remove takes one row out. A reset
from the source is the only thing that rebuilds.

### 4. A plain list draws blank rows in silence

`DataGrid.Follow` does `item as GridRow`. Hand the grid an ordinary list and every item
misses the cast, the strip is filled with null, and the grid draws the right number of
completely empty rows with nothing in the log. The skill already says a grid is given a
`GridRows`, but a rule that fails silently is not a rule.

**What it should do.** Wrap a source that is not a `GridRows` automatically, or throw
where it is set. Wrapping is friendlier and costs nothing, since the wrapper is what
sorting and grouping need anyway.

### 5. Header sorting is dead in the tree grid

`DataGridHeaderCell.OnPointerReleased` requires `FindAncestorOfType<DataGrid>()`.
`TreeDataGrid` is a `Tree`, so there is no such ancestor. A tree grid column carrying a
`SortKey` shows the hand cursor and the hover tint and does nothing at all when clicked.
It went unnoticed because the harness sets a `SortKey` on six flat grid columns and on no
tree grid column, in `Kitbash.Gallery/Views/Pages/GridsPage.axaml.cs`. Giving the tree
grid one is the check.

**What it should do.** The header cell asks whatever owns the columns, not a named
control type. That is the first thing to move onto the shared piece.

### 6. A file dialog from an edit template kills the edit

`DataGridCell.OnLostFocus` commits, guarded by `HasPopupOpen`, which walks the visual
descendants looking for four shapes: a context flyout, a context menu, an open combo box
and a button flyout. `ui:PathField` opens a native picker, which is none of those, and it
is the edit template for the Program column in `CustomToolsEditorView.axaml:68`. Browsing
for a program should close the editor out from under the open dialog.

The guard is a list of things that are allowed to steal focus, and that list can never be
complete. The question is not what opened, it is where the focus went.

**Prove it first.** Headless, with a control that moves focus to another top level window
the way a picker does.

**What it should do.** End the edit when focus lands somewhere else inside the grid, or
when the window regains activation and the focus is not in the cell. Focus leaving the
window is not the end of an edit.

### 7. An edit survives its own row being recycled

`DataGrid` holds `editing` and `edited` across container recycling. `GridCellStrip.Fill`
clears `IsEditing` and swaps the content, but the grid still believes that cell is open,
and `EndEdit` then calls `FindAncestorOfType<DataGridRow>()` on a container that now
carries a different row. Typed text is lost, or `IEditableObject.EndEdit` runs against an
item that is no longer on screen.

**What it should do.** Recycling a container that holds the open editor ends the edit in
the same place the container is untold, which is the rule `ui:Tree` already records. The
alternative, keeping the edited row realised, is more machinery for a case a person
causes by scrolling away from what they were typing.

### 8. Group headings are selectable from the keyboard

`GridGroupRow` marks a left press handled so a click never selects a heading, but the
`ListBox` still arrows the selection onto one, and `SelectAll` takes them too. The
footer's count and the pick all checkbox in `GridsPage` both then count headings as rows.

**What it should do.** A heading is not selectable in the selection model, not only under
the pointer. Arrow keys step over it and select the next data row.

### 9. Sorting is fragile in four ways

- `GridRows.Compare` falls back to `ToString` with a culture compare whenever the two
  values are not the same runtime type, so an `int` against a `long` orders as text.
- There is no natural ordering, so `item10` sorts before `item2`. For a tool listing
  files, ids and versions that is the common case rather than the odd one.
- There is no per column comparer and no nulls last choice, and `SortKey` is
  `Func<object, object?>` so every comparison boxes.
- There is no multi column sort, and a sort left on a hidden column stays in force with
  its indicator nowhere on screen.

**What it should do.** `GridColumn` gains a `Comparer`, and the default one is natural
ordering for strings and a typed compare otherwise. The sort becomes a list of columns
rather than one, with the header showing which key it is. Hiding the sorted column either
drops the sort or keeps saying so in the footer, and the footer is the only place that
can say it.

### 10. Attaching a pager turns paging on behind the caller

`GridPager` writes its own `PageSize` onto the rows as soon as `Rows` is set, so a caller
that set a page size on the rows first has it replaced by the pager's default of 100. A
page change also leaves the scroll where it was, so turning a page can land halfway down
the new one.

**What it should do.** The pager adopts the rows' page size when the rows have one, and a
page change scrolls the body back to the top.

### Smaller ones, worth fixing while nearby

- `GridRows.Toggle` rebuilds the whole list to open a group. `TreeRows` splices, and this
  should too.
- The collapsed set is keyed on the group key with default equality, so a key object
  rebuilt on each pass never stays collapsed. Any key that is not a value or a string is
  affected.
- The footer's words are hard coded English inside the control theme, so a tool cannot
  change them and nothing counts in the plural the way `Humanizer` would.
- There is no row activation event. A double click does nothing unless the column happens
  to carry an edit template, so "open the thing this row is" has nowhere to hang.

## Gaps

Stage 11 did not set out to build any of these. They are what a data tool asks for.

### No cell focus model. Built in phase 3, apart from Ctrl+Space

There was no current cell, so there was no Tab across cells, no arrow movement, no F2, no
type to edit, no Enter to commit and move down, no Home, End, Ctrl+Home, Ctrl+End, no
Page Up or Page Down, no Shift+Space or Ctrl+Space. Measured against the W3C grid
pattern, none of the keyboard contract was implemented. **Ctrl+Space is the one still
missing**, since selecting a column has nowhere to land until phase 4.

**This is the feature.** Everything else in this section is secondary to it, and most of
it depends on it. Avalonia's own TreeDataGrid already has a name for the last part of it,
`BeginEditGestures.TextInput`, which is worth borrowing rather than inventing.

The contract to build, taken from the pattern and confirmed against AG Grid and the
Windows Community Toolkit:

| Key | What it does |
|---|---|
| Arrows | move the current cell one step, stopping at the edges |
| Home, End | first and last cell of the row |
| Ctrl+Home, Ctrl+End | first cell of the first row, last cell of the last |
| Page Up, Page Down | a viewport of rows, keeping the cell in view |
| Tab, Shift+Tab | next and previous cell, wrapping to the next row |
| Enter | commit and move down. F2 and Enter both open an editor |
| Escape | cancel the edit and give navigation back |
| A printable key | open the editor and take that character as the first one |
| Shift+Space | select the current row |
| Ctrl+Space | select the current column |
| Ctrl+A | select everything |
| Shift and arrows | extend the selection |

Two of those need a decision rather than a lookup. Whether Tab wraps to the next row is a
data entry convention and a grid that refuses to wrap makes a person reach for the mouse
at the end of every row. And whether Enter moves down or restores navigation depends on
the editor, since a multi line editor wants the key.

### No clipboard

No copy, no cut, no paste. The settled convention across every grid read: tab separated
and Excel compatible, headers optional, paste anchored at the current cell and repeated
to fill a selected range when it divides evenly, cells that cannot be edited refused
rather than skipped silently.

A tool where a designer pastes forty rows out of a spreadsheet is the reason this app
exists, so this is not a nicety. Copy is on by default and the three that write are not.
See `CellActions` under What is optional.

### No cell range selection and no fill handle

Row selection is all there is. A range is what makes copy worth having, what a fill
handle drags over, and what a status readout aggregates. Both are behind
`SelectionUnit` and `CellActions`.

### No row level edit transaction

`IEditableObject` is called around one cell: `BeginEdit` when the cell opens, `EndEdit`
or `CancelEdit` when it closes. The WPF model is two level, where the row edit opens on
the first cell touched and closes when focus leaves the row. Without that there is no
cross field validation and no way to cancel a whole row, and a row that is only valid
once three fields agree cannot be expressed.

### No validation surface

Nothing reads `INotifyDataErrorInfo` and there is no cell error state. Consumers hand
roll it, which `CustomToolsEditorView.axaml:45` does with a class and a tooltip. A
library that leaves this to every caller gets a different answer in every tool.

### No in cell form for any editor

A control dropped into a cell unchanged looks like a control that fell into a cell. The
design's own words. Every editor needs a second form for when it is the whole content of
a cell: it gives up its own shape, fills the cell to all four edges, and earns its chrome
only as the pointer gets closer. At rest a column of them has to read as data, because a
grid showing eight visible widgets per row cannot be scanned.

Nothing in the library has that second form today, and the launcher's editors show what
happens without one. `CustomToolsEditorView` puts a whole `ui:PathField` in a cell with
`IsCompact` and a `TextBox` in another, each keeping its own border and radius inside a
31px row.

This is the tenth design and it is the one with the most surface, since it touches eight
control types. It is the section named Controls inside a cell.

### No filter in the row model. Built in phase 2

`GridRows` sorted, grouped and paged. A search box meant rebuilding the source, and then
`Total` was the filtered count, so the footer's reading of how many rows are shown out of
how many there are stopped being true. `GridRows.Filter` now sits beside the sort, and
`Total`, `Matched` and `Shown` are three separate counts.

### Column work missing

- No double click on a divider to fit the contents. It is the most used gesture in any
  grid and we do not have it. It measures the realised rows only, which is what a
  virtualised grid can honestly do.
- No reorder by dragging a header.
- No pinning. See Column pinning under What is optional, since it has rules of its own.
- No column chooser.
- No keyboard resize.
- No persistence. Widths, order, visibility and pins are per person and per machine, and
  `IDockLayoutStore` is the shape to copy.

### No empty state

Zero rows draws nothing at all. "Nothing here yet" and "nothing matches that filter" are
different messages, and the second one needs a way back.

### No default text handling

Every cell template writes its own trimming. A header cell has none and simply clips mid
glyph, since the theme only sets `ClipToBounds`. Truncated text has no tooltip unless a
caller remembers `ui:TextTip`.

### No accessibility

There are no automation peers, so a screen reader hears a list box full of content
controls. The pattern asks for a grid, rows, column headers and cells, with the sort
state, the column and row indices and the total counts published. Sorting a column
announces nothing.

## What is optional

### The test

**A feature is opt in when it writes, or when it changes what an existing gesture already
means. Everything else is on.**

That is the whole rule and it decides every case below. It is also what the library
already does. `TextDiff.Picking` defaults to `None` because staging writes. Pagination is
opt in because attaching a pager changes what the body shows. Docking is a second style
include because it costs fifty dictionaries. Nothing here is optional merely because
somebody might not want it, since every switch is a way for two Kitbash tools to behave
differently, which is the thing this library exists to stop.

**Optional means it costs nothing when it is off**, not that it is built and hidden. A
grid with no paste installs no paste handler. A grid with nothing pinned lays out through
one panel in one pass. That is a constraint on the implementation, not only on the API.

### Five properties, not fifteen booleans

The switches are grouped, so a tool sets a handful of modes rather than a field per
feature. All five live on the shared piece so both grids carry them, declared once and
given to the other with `AddOwner`, which needs checking against Avalonia 12 before it is
relied on.

| Property | Values | Default |
|---|---|---|
| `SelectionUnit` | `Row`, `Cell` | `Row` |
| `BeginEditGestures` | `None`, `DoubleTap`, `F2`, `Enter`, `TextInput` | `DoubleTap, F2, Enter` |
| `EditUnit` | `Cell`, `Row` | `Cell` |
| `CellActions` | `None`, `Copy`, `Cut`, `Paste`, `Clear`, `Fill` | `Copy` |
| `ColumnGestures` | `None`, `Resize`, `FitToContents`, `Reorder`, `Hide`, `Pin`, `MultiSort` | `Resize, FitToContents` |

The last three are flags sets, so a tool that wants two of the four gestures does not have
to give up all of them.

**Every default is what the grid does today**, apart from the two things it does not do
yet and should, which are fitting a column to its contents and copying. So no existing
consumer changes behaviour when this lands.

**A gesture needs two gates, never one.** The grid says a person may do this at all, and
the column says this column allows it. Resize is the pattern already: `ColumnGestures`
carries `Resize` and `GridColumn.CanResize` refuses it for one column. Sorting works the
same way through `SortKey`, and editing through `EditTemplate`.

**These are the author's decisions, written in a tool's markup.** None of them is a
person's setting. The one thing that is per person is column state, and that is covered
below.

**`BeginEditGestures` of `None` is the read only grid**, so there is no separate switch
for that.

### What that makes optional

- **Paste, cut, clear and fill** all write, and all write to more than one cell at a
  time. They arrive through keys a person hits while meaning something else. This is the
  same call `Picking` makes for staging a diff, and it matters more here, since a Godot
  scene is a format whose lines reference each other and a designer pasting forty rows
  into the wrong column of a live data file is exactly the failure the default prevents.
  Copy stays on, because reading is not writing.
- **Cell range selection** changes what a click and drag means. In a grid where the row
  is the unit, which is every grid in the launcher today, dragging extends the row
  selection. So it is a mode, `SelectionUnit`, and row is the default. Avalonia's own
  TreeDataGrid makes the same call.
- **Column reorder** gives a meaning to a drag on a header that currently has none, and
  it interacts badly with two things we have: a view that puts the pick column first, and
  the tree grid's `LeadColumn`, which is the column that indents.
- **Hiding a column** can break a view that depends on it, so the author says yes, not
  the person.
- **Multi column sort** makes Ctrl and click do something that one caret in the header
  cannot report. It cannot ship before design 5 says how a second key reads.
- **Type to edit** is what data entry people expect and it is also a stray keystroke
  starting an edit. It is in the gesture set so a tool can take it without taking the
  other three, or the other three without it.
- **The row edit transaction** changes when `EndEdit` fires and only means anything for
  an item that implements `IEditableObject` properly, so `EditUnit` stays on `Cell`.
- **Column pinning**, which has rules of its own below.

### What is deliberately not optional

- **Keyboard navigation and the current cell.** A grid that arrows in one tool and not in
  another is the failure this library exists to prevent. No switch is needed because the
  current cell mark only draws on keyboard focus, so nothing changes for a person who
  never presses a key. **The design confirms this**: keyboard focus adds the soft halo
  outside the border and pointer driven focus does not, so no switch is needed.
- **Fit to contents on a divider double click.** It does not write and it takes no
  gesture away from anything. It measures the realised rows only, which is what a
  virtualised grid can honestly do.
- **Validation marks, text trimming, overflow tooltips and the automation peers.** These
  are defaults rather than features. A mark that appears only when there is an error is
  invisible the rest of the time, a tooltip that appears only on text actually cut off is
  not noise, and a template that wants to wrap overrides the trimming.
- **Sorting, grouping, filtering and inline editing.** All four are already optional in
  the right way, which is by absence rather than by a flag. No `SortKey`, no sort. No
  `EditTemplate`, no edit. No group key, no groups. Keep that shape and add no boolean
  beside it.
- **The empty state.** Optional by nature, since with no content there is nothing to
  draw, and the words belong to the tool. The docking skill settles this exact question
  for `EmptyContent` already: what a dock with nothing in it says is the tool's, and only
  its tone is set here.

### The header menu falls out of this

The menu is the union of what is turned on. Sort appears when the column has a `SortKey`,
group by when the grid can group, hide when `ColumnGestures` carries `Hide`, pin when it
carries `Pin`. **A grid with none of them has no menu at all**, so the menu needs no
switch of its own.

### Column pinning

Pinning is opt in twice, because there are two decisions. `GridColumn.Pin` defaults to
`None`, which is an author freezing a column. `ColumnGestures` carrying `Pin` is whether
a person may change that from the header menu, and it is not in the default set.
Avalonia's own TreeDataGrid defaults `CanUserResizeColumns` to false for the same reason.

**Nothing pinned means nothing changes.** Pinning splits a row into a frozen strip and a
scrolling strip. The easy version builds both panels always and leaves one empty, and it
must not. With no pinned column, `GridCells` measures and arranges exactly as it does
now, one panel, one pass, no clip, no second offset and no seam. The split comes into
existence when a column asks for it.

**The seam only exists when it is doing something.** It draws while there is a frozen
strip and the body has actually been scrolled past it, so a grid that never pins never
renders it and the design does not leak into every table.

**A pin is only stored when it is not the default**, so a grid nobody has pinned anything
in writes nothing about pinning.

### Column state is the one per person part

Widths, order, visibility and pins are a person's, kept per machine the way a dock layout
is. The opt in there is not a boolean: **no state key, nothing stored.** A grid that names
no key cannot collide with another grid, which is what a global default would cause.
`IDockLayoutStore` is the shape to copy.

### The plain rendering stays, and it is a look rather than a behaviour

**Behaviour and rendering are two axes and they compose.** The five properties above say
what a gesture does. `plain` says what the table looks like. Every behaviour set has to
work in either rendering, and neither axis may be read off the other.

**The ruled grid is the primary case.** It is what the design draws, it is what a grid is
for, a document with chrome of its own, and it is what a decision is made against first.
`plain` follows the decision rather than shaping it.

There is one thing here not to misread. The only three grids in the launcher today are
all `plain`, in `CustomToolsEditorView`, `ToolRepositoriesEditorView` and
`WorkspaceLinksEditorView`. That is because the settings list editors were the first
things to need a table, not because plain is the norm. A tool built on this library will
be ruled far more often than plain.

**It stays a class and does not become a property.** A class is what the library uses for
an alternative look, and `plain` already lives as forced descendant styles outside both
control themes, because a `ControlTheme` may not hold a descendant selector. Any new
style that varies by rendering goes in the same place, beside the `IsMono` and `IsStrong`
rules.

**Plain is scoped, and that is what keeps it cheap.** It is a table on a page rather than
a document, so it does not get the features that assume a document:

| Not in plain | Why |
|---|---|
| pinning | a plain grid drops the header fill, so a frozen strip has nothing to read against and the seam has nothing to separate |
| the header menu | its contents are sort, group, hide and pin, and a table on a page is not where those are driven from |
| the pager | a page of a table that is already inside a page |
| cell range selection | plain is a way of looking at a list, so the row stays the unit |

This is the same decision made once more rather than a new rule. Plain already drops the
three things that make a grid read as a document, so it dropping the features that assume
one follows from that.

**The refusal cannot be a throw, because the class is dynamic.** `plain` is a class and a
class can be set at runtime, which `GridsPage.axaml.cs` does to toggle the two renderings
live. So a feature that plain does not carry stands down while the class is on and comes
back when it is off, rather than failing. A pager attached to a plain grid draws nothing
and pages nothing. A column with a `Pin` keeps the value and does not act on it. This is
the one place a switch is read off the rendering, and it is worth the exception because
the alternative is drawing something that makes no sense.

**Two things still need an answer in both renderings.** A ruled row is flush against its
neighbours and against the frame, and a plain row has the control radius, a margin and
space around it, which is why focus on a ruled row is `FocusLine` inside it while a list
row takes the halo. The two are the current cell mark and the cell error mark, and both
are about a cell rather than a row edge, so the design's answer may serve both. The empty
state is a third but a mild one, since the words are the tool's and only the frame around
them differs, though the design's rule that the toolbar and header stay on screen has to
hold in plain too.

## The structural move

Before any of the features, the shared piece has to own the state. Today `GridFrame`
owns the column resolve, the header offset and the relayout calls, while `DataGrid` owns
the sort, the edit and the counts, and `TreeDataGrid` owns none of that.

Move the sort, the edit state, the current cell and the selection mapping onto the shared
piece, with both grids delegating. The header cell then asks the owner rather than
looking for a `DataGrid` ancestor, which is defect 5 fixed rather than patched.

This is not a rewrite. `IGridRowLayout` shows the shape already: a small interface the
row implements, and the frame calling it. The same trick works for the rest.

## The design

**`Theme Slate - Data Grid.dc.html` in the Workbench project is the source of truth for
all of this**, read through DesignSync. **The grids have left
`Theme Slate - Surfaces.dc.html`**, which is where they were drawn for stage 11 and which
now holds the tree, docking, the overlays and the scroll bars, so this page is the only
place a grid is drawn. It reads whole at 186 KiB, under the 256 KiB cap that truncates
without failing, and it opens with the same two grids stage 11 built before going on to
ten sections that are new.

Read the page before building any of these. What follows records the decisions and the
values that are hard to find again, not the drawings.

### What it settles, section by section

**The current cell.** One square accent border on the cell edge, one pixel, and no fill at
all, because fill means range. It is the same mark a range of one cell wears, so nothing
changes shape when a selection grows. **The row gives up its focus line while a cell holds
the mark**, which is how a row avoids wearing a fill, an outline and a halo at once. On a
selected row both stay, since row selection says which records you picked and cell focus
says where the keyboard is, and the border lightens to `#9cc6ff` to clear the tint.
**Cell marks are the one square cornered thing in the theme**, because a cell is a
coordinate rather than a control. Keyboard focus adds the usual soft halo outside the
border and pointer driven focus does not.

**Range selection and the fill handle.** The same border moved out to the edge of the
block, plus a `rgba(86,158,255,.10)` wash on the cells inside it. **The anchor keeps the
plain surface and gets no ring**, so the one cell that still takes typing is marked by the
absence of a wash rather than by a second ring. The handle is 8px, centred on the bottom
right corner, half in and half out. Dragging it previews with a dashed `#569eff` edge and
the wash only lands on release. Shift and click extends from the anchor, Ctrl and drag
adds a second block, and a drag over the header takes the whole column. **The footer swaps
the row count for range aggregates while a range is live**, count, sum and average.

**A cell in error.** Fill `#3a2325`, hairline `#7d3c3f`, corner mark `#ef6a6e`, so the
state survives a colour blind reading and a greyscale screenshot. **Error beats focus for
the colour of the ring**: a current invalid cell steps its hairline up to full `#ef6a6e`
rather than taking the accent outline, and while editing the field border and the focus
halo both turn with it. **The message is never inline.** It arrives in a popup under the
cell when that cell is current, other invalid cells stay quiet until reached, and the
footer counts what is blocking a save. A row that is invalid as a whole, rather than in
one field, marks the state column and puts the message in the row detail, because there is
no one cell to blame.

**Empty states.** All three keep the toolbar and the column header on screen, since the
grid is a place rather than a page and the control that gets you out of the state has to
stay where it was. Each says one sentence and offers exactly one action. Nothing yet is
the only one that teaches. No matches repeats the filter that emptied the grid beside the
way to clear it, so the cause and the cure are in the same place. Loading is skeleton rows
at the real row height so nothing moves when the data lands, flat `#2b2d31` with no
shimmer, and a count as soon as the query knows one. **No spinner sits in the middle of a
grid**, because a grid can reload on every keystroke of a filter.

**The column header menu.** The whole header is the button: the label sorts and a chevron
on the right opens the menu, so the common action never costs a menu. Seven items, in
order: sort ascending, sort descending, clear sort on this column, group by this column,
pin to the left, filter on this column, hide column. The header keeps its hover fill while
the menu is open. **An unsorted header reveals a muted caret on hover**, which we do not do
today and which is the cheapest thing on this page to add. For a second sort key the caret
gets a small ordinal beside it and both columns keep the bright label, and the footer
spells the order out in words, where clicking a name drops that key. Past three keys the
footer stops naming them and says how many more. **Shift and click on a header adds a key
rather than replacing one, and it is the only place in the app where shift and click does
not extend a selection.**

**The pinned column seam.** At rest it is the ordinary `#33353a` hairline, because nothing
is hidden yet. Once content scrolls underneath, the hairline stays and a 12px shadow falls
from it, and it fades back out at offset zero. **That is the only place in the theme where
a shadow appears inside a panel**, which is worth knowing before somebody reaches for one
elsewhere. Pinned columns keep the row fill of the row they belong to, tint and hover
included, so a selected row never looks split in two. The menu offers pinning to the left
only.

**The column chooser.** A list rather than a grid of checkboxes, because order matters as
much as visibility and a list can be dragged. Pinned columns are a group at the top rather
than a flag hidden in each row. **Two columns can never be turned off**, the key and the
state, and they say so by sitting flat and disabled rather than leaving the list. Changes
apply as they are made, so there is no apply button and closing the popover is not a
commit. The drop line while dragging is the same dashed accent the docking targets use.
**Reset returns the workspace default, which is a saved layout rather than a hard coded
one**, so column state has two layers and not one.

**The selection action bar.** It floats over the last rows rather than pushing them, so
nothing reflows when a selection starts. It carries the count, the actions for the whole
set, and a way to drop the selection. **The footer gives the count back to the query once
the bar owns the selection**, so the same number is never printed twice. Actions that
cannot apply to every picked row are shown and disabled with the reason in the tooltip,
because a bar that changes shape as a selection grows is unreadable. Delete is a text
action in the error tier, since the solid fill belongs to the confirm dialog after it.
Escape drops the selection.

**The editable cell hint.** Nothing at rest, because an affordance printed on every
editable cell is printed on almost every cell and says nothing. It arrives in two steps:
row hover underlines the cells that accept a value with a one pixel `#3d4045` line inside
the cell, which is a seam colour and not an accent, and cell hover closes that underline
into a full `#43464e` border, the same one a text field wears at rest. **A read only cell
simply never reacts, and that difference is the whole signal.**

**The modified mark comes back, and stage 11's departure is overturned on purpose.** Stage
11 dropped it because the state column already said modified and two marks for one fact is
noise. The design agrees that holds while the state column is on screen, and points out it
stops holding the moment that column is hidden from the chooser, scrolled past on a wide
grid, or replaced by a grouping. So there are three marks answering three different
questions: a 6px bar on the row's left edge for which row, pinned with the first column so
it cannot scroll away, a ten percent `#e0a94a` wash with a corner mark at the bottom left
for which field, which is where the range handle never sits, and the state column's own
dot and label for what kind of change. Saving clears the wash and the bar in the same
frame with no flash of green. **A row that fails to save keeps the bar and adds the error
dot, which is the only time amber and red appear on one row.**

### Controls inside a cell

The tenth section, which was not on my list, and the one with the most surface because it
touches eight control types: boolean, enum, number, text, reference, tags, ratio and
colour. Each gets a second form for when it is the whole content of a cell.

**Every cell is 31px and flush to its column edges. Nothing is inset**, so the hit target
is the cell and never a smaller shape drawn inside it.

Three rules hold the whole set together and they are what to check any ninth type against:

1. **Chrome is earned in the same three steps everywhere.** A muted hint on row hover, a
   `#43464e` edge on cell hover, the accent field while editing.
2. **Nothing inside a cell takes the 5px control radius, because the cell is the shape
   now.** The only radius left is on chips and the checkbox, which are content rather than
   chrome.
3. **Alignment follows the column and the affordance takes the opposite edge**, so a
   chevron never lands between two numbers.

Per type, the part that is not obvious from those three:

| Type | The rule |
|---|---|
| boolean | a tick, never a labelled checkbox. The whole cell is the target and space toggles it |
| enum | reads as plain text until the pointer arrives, and the chevron takes the far edge so the values still line up |
| number | right aligned mono so the decimal points stack. The stepper exists on hover only, inside the cell edge |
| text | nothing at all at rest. The cell hover edge is the only promise that a click puts a caret here |
| reference | a type glyph and the name, with a jump button on hover at the far edge. **The only in cell button in the set** |
| tags | chips keep the 8px pill radius, because they are content and not chrome. Overflow counts rather than wraps, since the row height is fixed |
| ratio | the bar is a cell background, edge to edge and square, so it never reads as a widget sitting on the value |
| colour | the swatch is square and cell height less the padding, so the column reads as a strip of colour down the grid |

**This is where the library's existing controls get reused rather than replaced.** A chip
is `ui:Chip`, a colour well is `ui:ColorField`, a stepper is `NumericUpDown`. What is new
is the in cell form, which is a keyed theme per type in the same place the twelve
alternative styles already live, not a new control each.

### What plain still needs

The design draws the ruled grid throughout, which is consistent with the ruled grid being
the primary case. Two of these still need an answer in plain, and both are about a cell
rather than a row edge, so the design's answer may carry across unchanged: the current
cell mark and the cell error mark. The in cell control forms should carry across as they
are, since they are about the cell being the shape and a plain grid's cells are the same
shape.

## Order of work

Each phase is verifiable before the next depends on it, and the early ones are small.

**1. The defects. Done.** Eight of the ten, each with a headless test written first, four
proved a second way by putting the old code back.

**2. The row model. Done.** One wrapper per item, a rebuild that splices rather than
replaces, the selection and the scroll position held across a sort, source observation,
a filter predicate, a per column comparer, natural ordering and multi column sort. Defects
2, 3 and 9 are answered here rather than patched.

**3. The structural move. Done.** Then **the cell focus model** and the keyboard contract,
**done**, which brought `BeginEditGestures` and drew the current cell mark and the editable
cell hint. **What is left of this phase** is the edit lifecycle as a two level transaction,
which brings `EditUnit`, and the modified mark at row scope.

**4. Copy, then range selection, then the three that write, then fill.** Brings
`SelectionUnit` and `CellActions`. Draws the range wash, the anchor, the handle and the
footer's range aggregates.

**5. Column power.** Fit to contents, reorder, pin, the chooser, and persistence in two
layers, since the chooser's Reset returns a saved workspace default. Brings
`ColumnGestures` and the column state key. Draws the header menu, the multi key ordinals
and the footer's spelled out sort, the pinned seam, and the chooser.

**6. Controls inside a cell.** The eight in cell forms, as keyed themes over controls the
library already has, beside the twelve alternative styles. It sits here because the three
steps of chrome it uses are row hover, cell hover and editing, and the first two of those
only exist after phase 3.

**7. The rest.** Validation surface, empty states, the selection action bar, trimming and
tooltips by default, and the automation peers. Brings no switch at all, which is the point
of that group.

**No phase adds a switch that is not in the table under What is optional.** If a feature
turns out to need one that is not there, that is a sign the rule was applied wrongly and
the feature should be looked at again before a sixth property is added.

**Phases 1 and 2 needed nothing from the design and are done. Phase 3 is next.**

## Not in scope

**Column virtualisation.** Stage 11 left it out and it stays out. Seven columns over nine
realised rows is sixty three cells, which is fine, and the case that would hurt is a grid
with fifty columns that nothing has asked for yet. Leave room in the column model rather
than building it.

**Variable row height.** `variable-height-list.md` already measured what
`VirtualizingStackPanel` gets wrong with uneven rows and it is a panel of its own. Grid
rows are one height today so the grid does not need it yet. A wrapped cell, a row detail
or a tree grid row that opens all meet it immediately, so build that panel before any of
those, not as part of this.

**Undo.** It belongs to the tool holding the data, not to the control drawing it. A grid
that keeps its own undo stack will disagree with the tool's the first time both exist.

## Where phase 1 departed from this plan

Five things came out differently once the tests were written. The plan was wrong in each
case and the code is right.

**Defect 1 needed no new API.** The plan proposed `SelectedData` and `SelectedDataItems`.
Avalonia's `SelectingItemsControl` already has `SelectedValue` and `SelectedValueBinding`,
which is the platform's own answer to a list of wrappers and which carries both
directions. So it is one line per control, as an overridden default rather than a local
value, so a theme or a caller can still replace it:

```csharp
SelectedValueBindingProperty.OverrideDefaultValue<DataGrid>(new Binding(nameof(GridRow.Item)));
```

Consumers bind `SelectedValue`. **`ui:Tree` took the same line**, since it had the
identical trap with `TreeRow`, and that is what gives the tree grid the behaviour. A test
confirms an overridden default reaches a derived type rather than assuming it.

**Defect 6 was wider than described.** The plan blamed a native file picker. Measured, the
edit ends whenever the focus leaves the window at all, including for any second window and
for the focus going nowhere. So the fix is not a better list of popup shapes, it is the
rule the plan asked for: **where the focus went decides, never what opened.** Three cases,
and the `HasPopupOpen` denylist is gone:

- nowhere at all, which is the app losing focus, keeps the edit
- inside the cell, walking the **logical** tree so it crosses into a popup the cell
  opened, keeps the edit
- another window keeps the edit, and only another element in the same window ends it

That also made the old popup test wrong. It moved focus to an unrelated button as a stand
in for opening a dropdown, which is not what opening a dropdown does. It now focuses a row
inside the dropdown and asserts that row is **not** a visual descendant of the cell, which
is what makes the logical walk the thing under test.

**Defect 5 became a feature rather than an honest refusal.** The plan expected the header
to stop offering a sort the tree could not do. Sorting a tree turned out to be cheap,
because `TreeRows.Build` is the one place items become rows and expanding goes through it
too. So `TreeRows.Sort` orders siblings under every parent, and a branch opened after the
sort takes it as well. `IGridSorting` is the seam the header cell asks, so no header names
a grid type any more.

**Defect 8 could not use the hook it wanted.** `SelectingItemsControl.MoveSelection` is not
virtual, so arrow skipping is done in `OnKeyDown`, taken only when the next row is a
heading and no modifier is held, which leaves extending a selection to the list. Select all
goes straight to the selection model and misses every override, so headings are taken back
out in `SelectionChanged`, which is the one place that catches every way in.

**Two of the smaller ones were deliberately left.** `GridRows.Toggle` rebuilding rather
than splicing, and the collapsed set keyed on default equality. Both live inside `Build`,
which phase 2 replaced outright, so fixing them in phase 1 would have been work thrown
away. Phase 2 closed both. The footer's hard coded English and the missing row activation
event are still open, and neither is a defect.

## Where phase 2 departed from this plan

**The rebuild does not always splice, and it does not need to.** The plan asked for a diff
into the existing list. `GridRows.Splice` does that, but only when the rows it keeps are
still in the order they were already in. A sort reorders everything it keeps, and no run
of inserts and removes describes that, so a sort goes through one reset. What matters to a
person is kept another way: **the selection and the scroll position are put back by the
grid**, held over the rebuild by `Rebuilding` and `Rebuilt`. Row identity is what makes
that possible, since the rows to pick again are the same objects they were before.

So the split is:

- **an add, a remove, a filter, a group opening or closing, a page turn** splice, and the
  rows they keep never leave their containers
- **a sort** resets, and the selection and the offset are restored across it

An insert really is one `Insert` event at the place the sort in force puts it, which is
what the plan asked source observation to do, and it falls out of the diff rather than
being written separately.

**Row identity is by reference, not by equality.** `GridRows` keys its wrapper map on
`ReferenceEqualityComparer`, so a row follows the identity of the item rather than its
value. Two records that compare equal are two rows, which is right for a grid, and the
same object listed twice gets a row each, which the map cannot hold so the second one is
made fresh each pass. **A source of boxed value types keeps no identity at all**, since
every pass boxes again, and that degrades to the old behaviour rather than breaking.

**The collapsed set takes a comparer rather than guessing.** `Group` gained an optional
`IEqualityComparer<object>` for its keys. There is no clever default that can tell a key
rebuilt each pass from one that means something different, so the caller says. The stand
in for a null key is kept away from that comparer, since a comparer written for real keys
has no reason to expect it.

**The subscription is weak.** `WeakEvents.CollectionChanged` from `Avalonia.Utilities`,
because a source usually outlives the grid drawing it and a strong handler would root the
whole row set through it.

**`SettingsListEditor.Announce` stopped rebuilding the grid.** Its rows are an
`ObservableCollection`, so the grid now hears for itself and the call was a second rebuild
doing nothing. The method still tells the page.

**The gallery's filter box was wired up rather than added.** It was already on the page
with a placeholder reading "Filter rows" and no handler at all.

**Filtering the tree grid is not built.** Everything else in phase 2 is `GridRows` alone.
A tree filter has to decide what happens to a branch that does not match but holds a child
that does, and that is a behaviour to design rather than a translation of this one.

**Two things phase 2 does not answer.** A selection does not survive a page turn, because
`SelectedItems` can only hold rows that are in the list and the other pages' rows are not.
And multi column sort has no way in from the header yet, which is phase 5 and the
`MultiSort` gesture. The model, the chained comparer and `GridColumn.SortOrder` are all
there waiting for it.

## Where phase 3 departed from this plan

**The structural move went further than the plan asked and paid for itself at once.**
`GridBody` is the shared piece, holding the edit state and the current cell, and both grids
own one. `IGridHost` is what a cell asks for it, so `DataGridCell` names no grid type at
all. **The tree grid could not edit before this.** It had no begin, no commit, no double
click handler, and `DataGridCell` looked for a `DataGrid` ancestor it would never find, so
every one of those was dead in a tree grid. It works now because the code is the flat
grid's, not because it was written twice.

**A double click in a tree grid had to be arbitrated.** `Tree` already answered it by
opening the row and marking it handled, so the cell never saw it. `Tree.Claimed` is the
hook: the tree grid takes the gesture when the cell under the pointer can be edited, and
the row opens in every other case.

**The current cell's row is the selected row, and the body owns only the column.** The
plan implied a row and a column kept together. Keeping one number is enough while
`SelectionUnit` is `Row`, it cannot drift from what the list thinks, and up, down, page up
and page down stay the list's own with the column carried along. Phase 4 is where the row
becomes independent, and that is the same phase that brings `SelectionUnit`.

**Home and End changed meaning, and that breaks the no default changes rule on purpose.**
They were the first and last row and they are now the first and last cell of the row, with
Control and Home reaching the corners of the grid. There is no way to have the keyboard
contract the plan wrote down and leave those two keys alone. Every other default is
untouched.

**Tab walks the cells and only leaves the grid at the last one.** The plan called the wrap
a decision rather than a lookup and came down on wrapping. So Tab moves a cell, wraps to
the next row, and falls through to the focus manager only when there is no next cell, which
is how a person still gets out.

**Left and right are the tree grid's one split.** They open and close the row in the column
that draws the caret and move the current cell in every other column, which is what
Avalonia's own TreeDataGrid does. The cost is that right on a leaf in the caret column does
nothing rather than moving on.

**The mark could not live in the control theme.** Avalonia refuses a descendant selector
inside a `ControlTheme`, so the two rules that read a row's state from a cell, the row
hover underline and the lighter border on a picked row, are plain styles beside the themes.
The cell scope rules stayed in the theme.

**Two things drawn that the plan listed under gaps.** The editable cell hint, in its two
steps, and the row giving up its focus line while one of its cells holds the mark, which is
the `:cell` state on the row.

**Not built yet in this phase.** The two level edit transaction and `EditUnit`, the modified
mark at row scope, and Control and Space for a column, which needs cell range selection and
so belongs to phase 4.

## Sources

- **`Theme Slate - Data Grid.dc.html` in the Workbench design project**, which is the
  source of truth for every visual decision here and outranks everything below it
- W3C ARIA authoring practices, the grid pattern, which is the keyboard contract
- AG Grid, for the clipboard format, cell selection, the fill handle and the header keys
- The WPF DataGrid and the Windows Community Toolkit DataGrid, for the two level editing
  transaction over `IEditableObject`
- Avalonia's own TreeDataGrid, for the names the platform already uses
- Pencil and Paper's enterprise data table analysis, for the conventions that are
  conventions rather than contracts
