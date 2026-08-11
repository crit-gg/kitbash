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

| # | What | Where | Proof |
|---|---|---|---|
| 1 | Selection hands back the wrapper, not the data | `DataGrid.cs`, three launcher views | needed |
| 2 | Every rebuild destroys row identity | `GridRows.cs:157` | read |
| 3 | The source is never observed | `GridRows.cs:159` | read |
| 4 | A plain list draws blank rows in silence | `DataGrid.cs:203` | read |
| 5 | Header sorting is dead in the tree grid | `DataGridHeaderCell.cs:59` | read |
| 6 | A file dialog from an edit template kills the edit | `DataGridCell.cs:105` | needed |
| 7 | An edit survives its own row being recycled | `DataGrid.cs:242` | read |
| 8 | Group headings are selectable from the keyboard | `GridGroupRow.cs:88` | read |
| 9 | Sorting is fragile in four ways | `GridRows.cs:289` | read |
| 10 | Attaching a pager turns paging on behind the caller | `GridPager.cs:119` | read |

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
It went unnoticed because the gallery sets no `SortKey` on the tree grid.

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
footer's count and the gallery's pick all checkbox both then count headings as rows.

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

### No cell focus model

There is no current cell, so there is no Tab across cells, no arrow movement, no F2, no
type to edit, no Enter to commit and move down, no Home, End, Ctrl+Home, Ctrl+End, no
Page Up or Page Down, no Shift+Space or Ctrl+Space. Measured against the W3C grid
pattern, none of the keyboard contract is implemented.

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

### No filter in the row model

`GridRows` sorts, groups and pages. A search box means rebuilding the source, and then
`Total` is the filtered count, so the footer's reading of how many rows are shown out of
how many there are stops being true. Filtering belongs beside sorting, in the same place,
so the counts stay honest.

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
  never presses a key. **Check this when design 1 arrives.** If the mark draws on a mouse
  click as well, this becomes a switch, and changing the design is the better answer.
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

## The structural move

Before any of the features, the shared piece has to own the state. Today `GridFrame`
owns the column resolve, the header offset and the relayout calls, while `DataGrid` owns
the sort, the edit and the counts, and `TreeDataGrid` owns none of that.

Move the sort, the edit state, the current cell and the selection mapping onto the shared
piece, with both grids delegating. The header cell then asks the owner rather than
looking for a `DataGrid` ancestor, which is defect 5 fixed rather than patched.

This is not a rewrite. `IGridRowLayout` shows the shape already: a small interface the
row implements, and the frame calling it. The same trick works for the rest.

## Designs needed

Each of these is a real visual decision and none of it should be invented in code.

1. **The current cell.** A cell level mark that coexists with the row's `FocusLine` and
   the selection tint, without a row wearing a fill, an outline and a halo at once.
2. **Cell range selection and the fill handle.** The range wash, the anchor cell, the
   range edge and the handle at its corner.
3. **A cell in error.** The mark on an invalid cell and where the message is read.
4. **Grid empty states.** Nothing yet, nothing matching a filter, and loading.
5. **The column header menu.** Sort ascending, sort descending, group by this, hide, pin.
   Plus how a multi column sort shows its order, since one caret cannot say second key.
6. **A pinned column's seam.** How the frozen edge reads while the rest scrolls past it.
7. **The column chooser.** The list of columns with toggles and a way back to the
   default.
8. **The selection action bar.** What appears when rows are picked. Today the footer
   carries a count and nothing else.
9. **The editable cell hint.** How a cell says it can be edited before somebody double
   clicks it, and whether a row with unsaved changes carries a mark. Stage 11
   deliberately dropped the modified mark, so this reopens that one decision on purpose.

## Order of work

Each phase is verifiable before the next depends on it, and the early ones are small.

**1. The defects.** All ten, each with a headless test, defects 1 and 6 proved before
they are fixed. Two of them are live in the launcher today. Needs no design.

**2. The row model.** One wrapper per item, a diffing rebuild that keeps selection and
scroll, source observation, a filter predicate, a per column comparer, natural ordering
and multi column sort. This is where defects 2, 3 and 9 are properly answered rather than
patched. Needs no design.

**3. The structural move**, then **the cell focus model** and the full keyboard contract,
then the edit lifecycle rebuilt on top of it as a two level transaction. Brings
`BeginEditGestures` and `EditUnit`. Needs design 1 and design 9.

**4. Copy, then range selection, then the three that write, then fill.** Brings
`SelectionUnit` and `CellActions`. Needs designs 1 and 2.

**5. Column power.** Fit to contents, reorder, pin, the chooser, and persistence. Brings
`ColumnGestures` and the column state key. Needs designs 5, 6 and 7.

**6. The rest.** Validation surface, empty states, trimming and tooltips by default, and
the automation peers. Brings no switch at all, which is the point of that group. Needs
designs 3, 4 and 8.

**No phase adds a switch that is not in the table under What is optional.** If a feature
turns out to need one that is not there, that is a sign the rule was applied wrongly and
the feature should be looked at again before a sixth property is added.

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

## Sources

- W3C ARIA authoring practices, the grid pattern, which is the keyboard contract
- AG Grid, for the clipboard format, cell selection, the fill handle and the header keys
- The WPF DataGrid and the Windows Community Toolkit DataGrid, for the two level editing
  transaction over `IEditableObject`
- Avalonia's own TreeDataGrid, for the names the platform already uses
- Pencil and Paper's enterprise data table analysis, for the conventions that are
  conventions rather than contracts
