---
name: kitbash-surfaces
description: "Kitbash surface rules. The depth ramp and Surface.Level/Nests, SurfacePanel and section labels, list and tree rows, ui:Tree virtualisation, both data grids and their column model, tabs, every floating overlay such as menus, flyouts, tooltips and popovers, and docking. Read before changing panels, lists, trees, grids, tabs, popups or anything docked."
---

### Panels and the depth ramp

The surfaces a tool is assembled from, and the rule that keeps them readable at any depth.

**The root tone is unique.** `SurfaceRoot` belongs to the window and its chrome, which is
the title bar, the rail, a status bar, a dialog footer and a `SplitView` pane. It never
appears again inside a window, so the outermost frame is always identifiable.

**Everything below it cycles three tones and starts over.** Level 1, 2, 3, then 1 again.
Three is the shortest cycle where a grandchild never matches its grandparent, and nothing
lightens as it descends. **Each tone carries its own seam**, so a surface takes a fill and
a border together, and `Themes/Surfaces.axaml` is the only place the pairs are written.

| Level | Fill | Seam |
|---|---|---|
| root | `SurfaceRoot` | `LineSeam` |
| 1 | `SurfaceNest1` | `LineSeam` |
| 2 | `SurfaceNest2` | `LineControl` |
| 3 | `SurfaceNest3` | `LineControlDeep` |

`LineControl` and `LineControlDeep` are named for controls and are not about controls at
all. Measured across the design's control inventory: every control on `SurfaceNest2` draws
`LineSeam`. The names stay because a popup edge is the same value.

**Controls are off the ramp.** A button stays on `SurfaceNest2` and an input well on
`SurfaceWell` with a `LineSeam` border at every level, so a field reads as a field wherever
it lands and only the container moves. No control theme reads any of this.

**Nothing counts.** `ui:Surface.Level` is an inherited attached property saying which tone a
place in the tree is on, and `ui:Surface.Nests` marks an element that starts a new one. A
nesting element reads its parent's level and holds one below it, so a panel moved to another
depth is right without being told. That is not a nicety: docking reparents panels in stage
12, and a depth worked out once and kept would be wrong the moment it moved.

Set `Nests` from a control theme, which is how `SurfacePanel` and `Expander` share one
behaviour without sharing a base type, or on a plain `Border` to make a region. Set `Level`
by hand only on a container that is not itself a surface. Never walk the tree to work a
depth out.

**`ui:SurfacePanel`** is a header over a body with a footer under it. It is a
`HeaderedContentControl`, which is already the first two, and the footer is the only member
added. `DialogFooter` is not reused: it sits on the root tone and aligns right, because a
dialog's action row is chrome and a panel footer is part of the panel.

**A panel is one tone throughout.** The header and the footer sit on the body's fill and are
told apart from it by a hairline alone, which is the seam belonging to the panel's own tone.
A fill change therefore means one thing, that a different panel has been entered. The design
draws one panel whose header and footer are `SurfaceRoot`, and that one is a window frame
rather than a panel.

The header takes whatever it is given. A string is drawn as the panel title at
`FontSizeBody` weight medium, and a layout is drawn as written, which is how a title keeps
actions or a note to its right without a second slot for them.

**`SectionLabel`** is the small capitalised label over a group, with a rule filling the rest
of the row and an optional note at the end. It is a keyed theme over
`HeaderedContentControl`, so the label is the header and the note is the content. Its rule
takes the seam of the tone it stands on, through `Border.seam`, which is worth reusing for
any hairline a view draws.

**`Expander.trailingCaret` moves the caret to the end of the header row.** For a header that
reads as a setting, label on the left and today's answer on the right. A caret in front of the
label pushes the labels of a stack of these out of line with every other row in the panel. The
header presenter still fills, so the value is content the view docks right inside the header.

**The splitter** is the hairline itself at rest, so the panes stay flush. Hover turns that
one pixel accent and dragging widens it to three, centred, so nothing shifts. A one pixel
line cannot be grabbed, so `GridSplitter` reaches seven and pulls itself back in with a
negative margin: an auto row or column then measures one while the bounds stay seven. Both
axes carry it, because `GridSplitter` resolves `Auto` privately and never writes the answer
back, so no theme can read which way it runs. Measured both ways.

**The empty panel** is a class rather than a control, `StackPanel.emptyState`, the way the
status bar readouts are. The design draws a neutral square where the glyph belongs, so the
view names the glyph.

### Rows, lists and trees

One row look, used by a list and by a tree, and the rule that keeps it readable.

**A row never carries both a fill and an outline.** Hover and selection are fills, focus is
the same halo every other control draws, and a drop target is the one dashed edge in the
theme. Selection is the tint alone, `AccentTint` with `SelectionInk` text at medium weight,
with no left marker and no border. A row wearing a tint, a border and a halo at once is what
this rule exists to prevent.

The seven states are in `Themes/Controls/List.axaml` over `ListBoxItem`, and everything else
here is built on them. Two of them are classes rather than states, because nothing in the
control says them: `modified` draws the amber square at the end of the row, and `drop` draws
the dashed outline. Drag and drop itself is stage 12.

A row is 27px in a list and 25px in a tree, both with 2px under them and the control radius
on the row itself. A list row rests at `InkSecondary` and comes up to `InkPrimary` under the
pointer, which is what the design's five columns draw.

**`ui:Tree` is a tree that virtualises, and it is ours because Avalonia has no such thing.**
`ListBox` is the only items control in Avalonia 12 that replaces its panel with a
virtualising one. `TreeView` does not, and neither does any `TreeViewItem` for its children,
so a tree of ten thousand nodes is ten thousand controls. **`TreeView` is deliberately not
themed**, so there is one tree in this library rather than two that behave differently.

```csharp
tree.ItemsSource = new TreeRows(roots, item => ((Node)item).Children);
```

`TreeRows` is the tree flattened to the rows that can be seen, and it is what a tree is
given. Expanding splices a subtree into the list and collapsing takes it out again, so the
list only ever holds rows a person could see. Collapsing forgets, and opening a row asks
what is under it again rather than trusting what it was told before.

Depth is a value on a row rather than a place in the tree of controls. `TreeItem` reads it
and turns it into three things: the frame is pushed in by `Level` times `IndentTree`, the
caret sits at the front of the frame and holds its width whether it is drawn or not, and the
guides are rendered by the row itself, since one element per level would be built and thrown
away on every scroll.

**A container is told everything in one place and untold in the same one.** `TreeItem.Follow`
is that place. A virtualising panel reuses a container for another row, so anything set when
one is prepared has to be unset there too. `ContainerIndexChangedOverride` is the case
usually missed, where a container is kept but moved. Measured over 10,200 rows: 303 recycled
rows read while scrolling, none wearing another row's state, with 7 or 8 controls realised
throughout.

**The indent is 14 and the guide falls at 8 into the band.** The design's prose says 14 and
its markup steps 21, and the two agree once the leading slot is taken out: a child there
swaps a 22 wide caret slot for a 15 wide mark, and 21 less 7 is 14. Every row here reserves
the caret slot instead, so a branch and a leaf that are siblings line up and the step is 14
outright. Measured: frames at 0, 14, 28 and labels at 30, 44, 58.

**A guide follows the tone it stands on** and stays one step quieter than that tone's seam.
The design draws a tree on the root tone, where the seam is `LineSeam` and the guide is
`LineRow`, and the ramp in the theme keeps that gap at depth. Measured, and the reason it is
not one brush: `LineRow` on `SurfaceNest2` differs by one in each channel, so a guide named
once disappears the moment a tree is put inside two panels.

**A click on the caret opens a row without changing what is picked.** The caret's hit area
handles the press and marks it handled, which stops the row selecting. A single click
elsewhere picks the row, a double click opens it, and left and right arrow do the same from
the keyboard: right opens a closed branch and steps into an open one, left closes an open
branch and goes out to the parent from a closed one.

**That hit area is the whole strip in front of the name**, not the glyph, so it is 29 wide
rather than 14 and runs from the row's edge to where the label starts. The caret pads itself
on both sides and the row pads only its right, which is why `PaddingTreeRow` looks lopsided.
Nothing moves as a result: measured before and after, labels sit at 30, 44 and 58 either
way. The indent band to the left of the row is not part of it, so clicking there picks the
row the way it does in every other tree.

**The strip answers a single left click and nothing else.** A right click and a middle click
are left to bubble, so a context menu on the row still opens over the caret. A double click
landing in the strip is swallowed rather than acted on, because two presses have already
toggled it twice and a third would leave the row where it started after flickering through
the other state on the way. Double click to open still works everywhere else on the row.

**Tabs** are `TabControl` and `TabItem` in `Themes/Controls/Tabs.axaml`, drawn from the
design's docking page with the dock's own tier left out. The open tab takes the page's
surface and the accent marker together, since either alone would be saying it in colour. A
tab is square, because it meets the page below it. Its label is mono, which is what the
design draws and is worth keeping, since a tab names a document rather than a sentence. Top
placement only.

**Whatever holds them needs two borders.** A tab strip fills its container corner to corner
and is square, so in a panel with a radius the strip and its accent marker paint straight
into the curve. `ui:SurfacePanel` is built for it: the outer border draws the stroke and an
inner one carries the radius and the clip.

`ClipToBounds` on a single stroked `Border` is not enough, which was measured rather than
reasoned about. A `Border` clips to its **outer** radius, so along a straight edge the child
sits inside the 1px stroke but at a corner it paints over it: the marker came through at
`#569eff` where the stroke should have been. Inset by the border thickness, the inner curve
clears the stroke by about 1.4px at 45 degrees, which is why two borders work and one does
not. The design says the same thing its own way, with `overflow:hidden` on the dock frame.

### The text diff

`ui:TextDiff` reads a diff as text. It is a read only `AvaloniaEdit` `TextEditor`, the way
SourceGit's is, so selecting and copying across lines is the editor's own. **The editor is a
substrate, not a look.** Its own line numbers are off and its rendering is replaced: three
gutters, a background renderer and a colouriser are what draw a diff.

`Avalonia.AvaloniaEdit` is MIT, published by the Avalonia team, and depends on `Avalonia`
alone. TextMate is a separate package and is not taken.

**Why an editor and not a list.** A list gives row selection, and a diff needs character
selection: dragging through half of one line and into the next, and copying exactly that. That
cannot be added to a list afterwards. Row selection is still there, as `SelectedLines`, which
is what staging a run of lines is picked with.

**A diff is a document, not a list of things.** Rows are flush against each other and against
the frame: no radius, no gap, no rule. What tells one line from the next is the mark down its
left, which is the one part of a row carrying colour at full strength.

**The document is the file's text and nothing else.** No `+`, no `-`, no line number in it.
Those are drawn in the gutters beside it, so what a person selects and copies is the code.

**Eight kinds and the theme is the only place they are written.** `Context`, `Added` and
`Removed` are what every diff has. `Ours`, `Theirs`, `Chosen` and `Settled` are what a merge
adds, and `Heading` is the row over a run. A kind is a `TextDiffKindStyle`, four things held
together: the fill, the mark, the ink and what a changed word sits on.

**Three gutters, each measuring itself off the font.** The number is measured against the
widest number in the file, so the gutter does not resize as the document scrolls past line 99.
The mark is `WidthDiffMark` wide. The symbol is the plus or minus in its kind's ink.

**The row height is a factor, not a number of pixels.** `FactorDiffLine` multiplies the font's
own line height, since the gutters are drawn against the same metrics. 1.33 over
`FontSizeControl` is the design's 21px row. Deliberately not in the density set: a diff is read
as a document and the design draws the same row at both.

**The changed words inside a line are worked out and lit.** `TextDiffWords` trims the shared
ends, runs a longest common subsequence over what is left, and joins runs that touch. Two
guards keep it honest: a pair longer than 400 words is left alone, since the comparison is
quadratic and nobody reads a line that long word by word, and a pair where over three
quarters of the line differs is left alone too, because marking nearly everything only
repeats what the kind already said.

`TextDiffWords.Mark` is what pairs the lines: a run of removals followed by a run of additions
is one replacement, paired off in the order they were written. It is on the word differ rather
than on the control, since what builds the lines is usually a view model with no control to
ask. A caller that wants no marking simply does not call it.

**The ink and the marked words are both run properties, not drawing.** `TextDiffColouriser`
changes a part of the line and the editor lays it out once. Only the row fill is drawn by
hand, since it has to span the whole width of the view rather than the width of the text.

**The seam for syntax colouring is `ITextDiffColouring`, and the library ships none.** A
grammar engine is a dependency and Godot's own formats would need one written anyway. The
control asks per line as it is built, so anything supplied has to be cheap. A grammar runs
before the changed words, so a marked word keeps its colour and only gains a background.

**Picking a run of lines is off until it is switched on.** `Picking` defaults to `None`, so a
diff that was never told stays read only. That default is the safety: a format whose lines
reference each other, such as a Godot scene, must never be staged a line at a time, and the
control cannot be made to offer it by accident.

**The run is the selection and nothing else.** Resting on a change offers nothing: a button
that appears wherever the pointer happens to be moves under the person reading, and it acts
on a run they never asked for. A person says what they mean by selecting it. A run holding
only context is never offered either, since there is nothing in it to stage. `Chunk` is the
answer and `Asked` is what a gesture raises.

**Every gutter selects whole lines.** A press on the numbers, the mark or the symbol takes
that line, and dragging takes every line it passes, from the start of the first to the end of
the last whichever way round it was dragged. Ending at the end is the point: without it the
press reaches the text area instead, which reads the pointer as being left of the text and
leaves the caret at the start of the last line, so that line is selected in name only. It is
on `TextDiffMargin`, so all three gutters behave as the one strip they look like. Character
selection in the text itself is untouched.

**`ui:TextDiffBar` is the buttons over the run**, placed in the same panel as the diff rather
than inside it, so the editor's own template is left alone:

```xml
<Panel>
    <ui:TextDiff Name="Diff" Picking="Lines" Actions="Stage, Discard" />
    <ui:TextDiffBar Diff="{Binding #Diff}" />
</Panel>
```

`Actions` says which of Stage, Unstage and Discard are drawn, and `Ask` refuses anything not
in it, so the gate holds whether the gesture came from the bar or from a key.

**The bar is the frame and its buttons carry none of their own.** `TextDiffBarButton` is
`CompactButton` with the background, the border and the radius taken off, so the bar reads as
one segmented control rather than as pills stacked in a box. A hairline sits between two
buttons that are both drawn and nowhere else, which `TextDiffBar.Show` decides along with the
buttons. Two borders, since the outer one carries the shadow that a clip would cut off and the
inner one clips the buttons to the rounded corner.

**The theme is `BasedOn` the editor's own `ControlTheme`.** `AvaloniaEdit.xaml` is included
before the resources in `KitbashTheme.axaml` for that reason. Do not reach for
`StyleKeyOverride` here: it makes a type selector match the base type, so `Selector="ui|TextDiff"`
silently stops matching and every token goes missing at once.

### Grids

Two of them, `ui:DataGrid` and `ui:TreeDataGrid`, and one column model under both. Both are
hand built by decision, which `.claude/plans/stage-11-data-grid.md` records and which is not
to be reopened.

**Neither grid is a control that holds rows. Both are the list and the tree already built.**
`DataGrid` is a `ListBox` and `TreeDataGrid` is a `ui:Tree`, so virtualisation, selection,
recycling and the seven row states are the ones from stage 9 rather than a second set.
Measured over ten thousand rows: nine rows realised holding sixty three cells, with the
stripe intact.

**One column model, `ui:GridColumns`, and it is the only place a width is worked out.** It
resolves against the viewport, so pixel columns take what they ask for, star columns take
what is left, and a set wider than the viewport scrolls rather than squeezing. **A width is
pixel or star and never auto**: auto has to measure every row, and a layout that only holds
while the whole set is realised is the one thing a virtualised grid cannot have.

`ui:GridCells` is the panel that puts one element per column at those offsets. A row, a
header and a tree grid row all use it, which is what keeps the three saying the same thing.
An element marked `GridCells.IsDivider` straddles the column's trailing edge instead of
filling it, which is how a resize reach sits over both sides.

**A grid is given a `ui:GridRows`, the way a tree is given a `TreeRows`.**

```csharp
grid.ItemsSource = new GridRows(items);
column.SortKey = item => ((Entry)item).Id;
```

`GridRows` is the source sorted, grouped, paged and flattened to what can be seen, and a
group header is a row in that list rather than a container around one. A column with no
`SortKey` cannot be sorted, so its title does nothing and draws no indicator. Sorting cycles
up, then down, then back to the order the source came in. The stripe is counted over data
rows alone, so a heading dropped in the middle does not flip it.

Paging cuts the page from the data rows and brings a heading along when any of its rows
landed on the page. Sorting and the counts read the whole set, so turning paging on changes
what is drawn and nothing else.

**The chrome sits on `SurfaceRoot`.** The toolbar, the header and the footer, all three. It
is the one surface other than a window frame that takes the root tone, because a grid is a
document with chrome of its own rather than a panel. The body rides the depth ramp normally.

**Focus on a grid row is a line inside it, not the halo.** This is the opposite of the answer
a list row takes and both are right. A list row is spaced and has room outside it. A grid row
is flush against its neighbours and against the frame, so the halo would land on them.
`FocusLine` is that value, and it is stronger than `FocusHalo` because it sits on the tint.

**A cell takes its ink from its row.** `DataGridCell` sets no colour of its own, so hover and
selection reach it by inheritance. Two things move it and both are about which column it is
rather than what is in it: `IsMono` swaps the family, and `IsStrong` says the column
identifies the row so it rests a step brighter. Both are plain `Style` rules outside the
control themes, because a `ControlTheme` may not hold a descendant selector.

**The tree grid is the tree with columns over it.** `TreeDataGridRow` is a `TreeItem`, and
only its `LeadColumn` indents. Name that column: it defaults to the first, and the design
puts a picker in front of the names. It steps 14 per level, the same as the tree, with the
caret slot held on every row.

It draws no stripe and no indent guides, both of which the design draws for neither. A branch
row takes `SurfaceRowAlt` instead, and its cells read at `InkMuted` with the name at
`InkPrimary`, so a branch reads as a summary of what hangs under it rather than as data.

**Inline edit is the column's `EditTemplate`, and the item is what remembers.** Double click a
cell that has one. The grid calls `IEditableObject` on the item, so `BeginEdit`, `EndEdit` and
`CancelEdit` are where a value is kept or put back, and Escape works because of that rather
than because of anything the grid holds. An item that does not implement it commits whatever
was typed.

An edit ends when focus leaves the cell, with one exception written into
`DataGridCell.OnLostFocus`: **a popup opened from inside the cell takes the focus with it**,
so the edit would end under whatever was opened. The cell holds on while a context menu, a
context flyout, a combo box dropdown or a button flyout inside it is open. Without that a
dropdown in an edit template cannot be used at all, since the list closes the cell that owns
it. `TextBox.OnLostFocus` guards its own selection the same way.

**`plain` is the other thing a table can be.** A grid is a document with chrome of its own,
and this is a way of looking at a list that is already on a page, beside a second way of
looking at the same list. It drops the three things that make a grid read as a document, the
rule under every row, the stripe and the header's fill, and its rows take the list's radius
and spacing instead. The header keeps its seam, since scrolling with no fill behind it that
seam is the only thing between the labels and the first row.

**It is a class on the grid and a set of plain styles, both forced.** A control theme may not
hold a descendant selector, so restyling rows from the grid has to live outside both themes,
beside the `IsMono` and `IsStrong` rules. And it is on the grid rather than on the row because
it is the whole table that is plain, and a view should not have to mark every row it builds.

**Pagination is `ui:GridPager`, a control of its own under the grid.** The grid does not know
pages exist. Attaching a pager is what turns them on, which is why a grid that never pages
carries none of it.

**Its four steps never move.** Two things are arranged for that and both matter, because a
button that walks out from under the pointer as it is pressed is the whole failure. The
numbers sit to the left of the steps rather than between previous and next, so a run that
changes width grows into the gap. The readout to their right holds the room its widest
reading needs, `RangeWidest`, drawn as nothing behind the real one, so five figures do not
shove the row along. Both rely on the readout being mono. Measured across pages 1, 2, 50, 99
and 100: the steps sat at the same four offsets every time.

**Do not base a grid theme on another grid's.** `TreeDataGridRow` is a `TreeItem` and
`DataGridRow` is a `ListBoxItem`, so `BasedOn` between them is accepted and silently ignored,
and the rows come out at their content height with no rule under them. `.claude/avalonia.md`
has the measurement.

### Overlays

Everything that floats lives in `Themes/Controls/Overlays.axaml`: menus, context menus,
flyouts, tooltips and the popover. They all sit on `SurfaceNest2` with a `LineControl`
edge at `RadiusControl`, with `ShadowPopup` for a menu or a tooltip and `ShadowOverlay`
for a popover.

**One radius for everything that floats**, the control radius. A popover is not a larger
card, it reads as an extension of the trigger that opened it.

`ui:Popover` is a header, a body and a footer. It is both the workspace list and the
shell a picker opens in, and its `inPanel` class drops the shadow and the surface so the
same body works dropped into a property panel.

**The room a shadow needs is part of the popup's window, and it has to answer for
itself.** A popup is a real window sized to what it holds, so the transparent room the
shadow falls into is window too, and the platform hands that window every click inside
its rectangle. `ui:Popups.Room="True"` on the element carrying that room takes a
transparent fill, which is what makes it hit tested, and closes the popup on a press that
lands on it rather than on the overlay inside it. So the room behaves as the outside of
the popup, which is what it is.

Without it a dropdown cannot be closed by clicking the control that opened it. The room
reaches 18px above the visible overlay and a control is 26px tall, so most of the trigger
is under the popup's own window, light dismiss never sees the click and nothing closes.
Verified against the backend rather than assumed: Avalonia's X11 popup is an override
redirect window with no input shape and no pointer grab, so nothing makes the transparent
part of it click through.

That is why the room is `Padding` on a wrapper rather than `Margin` on the card. A margin
is outside the element and belongs to nothing that can be clicked.

**A tooltip takes the same room with nothing above it.** `TooltipRoom` is `28,0,28,38`
against the menus' `28,22,28,38`, because a tooltip opens at the pointer rather than off a
control: Avalonia places it 20px below the cursor, so room above would put the popup window
back under the cursor and take the hover and the click that belong to the control the tip
came from. The shadow above the card is the part that is given up, and it is the faintest
part, since `ShadowPopup` is already offset 10 down.

**Its pull is the room's own left inset, read off the room.** `ui:Popups.PullsRoom="True"`
on the wrapper, not the offsets the other overlays take from the tokens, because a tooltip's
offsets are bound from the control the tip belongs to and a theme cannot reach them.
`MinHeight` and `MaxWidth` sit on the card for the same reason a margin does not work: on
the tooltip they would measure the room as well.

**A tooltip beside a control gets a different room, and `PullsRoom` works it out from the
placement.** Room facing the control is popup window laid over the control, which is the same
window that takes a click and takes the pointer with it: the control drops its hover, and a
pointer that has left the window closes the tip a moment later. So the side facing the control
is `TooltipGap` and nothing more, which is the standoff and all the shadow room there is on
that side. The card is then centred back onto the control, since a popup is centred across the
placement as a whole and the room under the card is deeper than the room over it.

`ToolTip.Placement="Right"` is what every rail item sets, and it is the only side placement in
the app. The four sides are all handled the same way, so a tip placed under or over a control
gets its standoff too.

**An overlay is at least as wide as what opened it.** A menu narrower than the button that
opened it reads as a mistake. `ui:Popups.MatchesTarget` puts a floor under the width, read
off the placement target the popup already holds, so an overlay with more to say is still
as wide as it needs to be. Menus and plain flyouts take it. A context menu does not, since
it belongs to whatever it was opened on and that may be a whole page.

**So a context menu is a `ContextMenu` and never a `MenuFlyout`.** The two look alike and
are not interchangeable. `ContextMenu` defaults to `PlacementMode.Pointer` and has a theme
here that takes neither `MatchesTarget` nor `Popups.InPopup`, so it opens at the cursor at
its own width. A `MenuFlyout` draws through `MenuFlyoutPresenter`, which is the dropdown
menu: it carries both, so `Popups.Place` pins it to the bottom edge of its target and holds
it to that target's width. Set it as `ContextFlyout` on a text field and it opens under the
field, as wide as the field. Measured: `ContextMenu` on a 420px field opened at the pointer
at 133px.

`MenuFlyout` is for a button's dropdown, which is what both of those behaviours are for.

**An overlay lines up with its target's left edge, and `ui:Popups.AlignsRight="True"` is the
only way out.** `Popups.Place` writes the placement itself and rewrites it whenever a
control asks for something else, so setting `Placement` on a flyout does nothing at all.
Put the property on the control the popup belongs to, never on the popup.

Use it for a control at the right of a row, where a left aligned menu wider than its button
runs away from the thing that opened it. The engine strip's Open in button is the one that
does. **The room pull is mirrored with it**, since the offset that cancels the shadow's room
is a leftward nudge and would otherwise hang the popup a room's width off the control.
Measured on a real popup: 28 left by default and 28 right with the opt out.

**A wheel inside an overlay stays in it.** A popup is its own window, but its child's
logical parent is the popup, which lives in the parent window's tree, so an unhandled wheel
routes out of the popup and scrolls the page behind it. Measured: a wheel inside the time
popover scrolled the page 150px and left the popover where it was, while the same wheel
inside a dropdown did nothing, because a scroll viewer there had already taken it. Nothing
was protecting the page, one popover simply had somewhere for the wheel to land.

`ui:Popups.KeepsWheel` on an overlay's root stops it. Bubbling does the deciding, so
anything inside that wants the wheel still gets it: measured after, an hour column still
picks by wheel and the page no longer moves.

**A dialog has no scrim.** It is a real window and the window manager owns modality. A
scrim belongs only to a popup that covers a page, and it is `ui:Dimmer.Dims="True"` on
that popup's flyout. The scrim goes in the window's overlay layer, so it needs the named
layer manager above, and the popup draws over it because a popup is its own window.

**An `ItemsPanel` setter is accepted and ignored.** Measured on a `ListBox`: the property
reports the panel that was asked for while the realised panel is the default one, from a
control theme and from a local value alike, because the presenter builds its panel once
and does not rebuild. Lay items out some other way rather than assuming it took.

A `BoxShadow` cannot be cleared by a setter with an empty value, which throws at layout
rather than at build. Use `ShadowNone`.


### Scroll bars

**The thumb is the whole control.** No stepper arrows, no filled track, no border, so
scrolling never adds a second edge beside the panel seam it sits inside. It never carries the
accent either, because position is not a selection.

**Four lanes, chosen by `ui:ScrollLane.Kind`.** The property is inherited, so it is set on the
thing that owns the surface and reaches the bars inside its scroll viewer's template, which a
view has no other way to touch.

| Kind | Lane | Thumb | For |
|---|---|---|---|
| `Panel`, the default | 12 | 6, `LineControl` | trees, lists, sidebars |
| `Tracked` | 14 | 6, `LineControl` | a surface that scrolls both ways |
| `Dense` | 8 | 4, `LineControl` | a pane too narrow to give twelve to a lane |
| `Well` | 10 | 4, `LineControlDeep` | code, logs, text areas |

The library sets three of them: `Well` on every text field, `Tracked` on both grids, `Dense`
on anything floating. A pane that is narrow for the same reason a popup is sets `Dense`
itself, since no selector can know how wide a view will be.

**Only a surface that scrolls both ways draws its lane.** The tracked lane sits on
`SurfaceRoot` with one `LineSeam` hairline against the content, so the two lanes meet cleanly
in the corner instead of leaving a hole. Every other lane is transparent and the thumb floats
on the panel, which is what the 5px padding on a list keeps rows clear of.

**A well's thumb is a rung brighter**, because the fill under it is darker and it has to read
at the same strength.

**Hover is the whole lane, not the thumb**, so the target is the full width even though only
the middle of it is drawn. A press holds its tone for as long as the drag does, including
while the pointer is off the lane sideways, which `Thumb` already reports.

**There is no disabled scrollbar and no empty lane holding space.** A surface that fits its
content draws nothing at all. Measured: content is 150px wide whether or not the list
overflows, because Avalonia overlays the bar rather than giving it a column, so a panel never
shifts by a lane's width when a list grows past its box.

**A grid is the one surface that takes the lane off its own width**, and it has to.
Everywhere else the content is as wide as it likes and an overlaid bar sits over the end of
it. A grid's star columns are sized to fill the viewport exactly, so the lane lands on the
last column and covers its values, its edge and the current cell mark. `GridFrame.Resolve`
subtracts the vertical bar's width whenever it is up, which is why it watches the extent as
well as the viewport: the viewport does not move when an overlaid bar appears. The header
strip still spans the full width, so a scrolling grid shows a lane's worth of header beside
the last column, which is what a grid is supposed to look like.

Thumbs take `RadiusControl` like every other control and clamp to half their own width.

### Docking

Dock for Avalonia under a Slate theme. It is the one third party control package in the
repository, MIT, and the exception to the rule that a control is an Avalonia type with a
theme over it. Everything Dock builds on underneath, a `Button`, a `ScrollViewer`, a
`MenuItem`, still takes the theme already written for it.

**Docking is a second include, not part of `KitbashTheme`.**

```xml
<StyleInclude Source="avares://Kitbash.Ui/Themes/KitbashTheme.axaml" />
<StyleInclude Source="avares://Kitbash.Ui/Themes/KitbashDocking.axaml" />
```

The launcher docks nothing and takes only the first, so it never carries Dock's fifty
dictionaries to draw a workspace list. `KitbashDocking.axaml` holds Dock's Fluent theme and
`Themes/Controls/Docking.axaml` over it. **A Styles collection is searched from its last
child back**, so the file listed after the Fluent theme is reached first, which is what lets
it replace a packaged resource.

**Dock carries a token layer of its own** in `Accents/Fluent.axaml`, about 150 keys covering
brushes, sizes, paddings and radii. Most of the Slate look is overriding those keys, and only
the parts with no token behind them replace a template. **A Style beats a ControlTheme**, so a
template is replaced with a `Style` holding a `Template` setter rather than by writing the
whole theme out again. Everything Dock's theme sets that the design does not disagree with is
then still set, including the styles it puts on its own template children, so an override has
to name any of those it wants back.

**Four templates are replaced and the rest is tokens.**

| Replaced | Why |
|---|---|
| `ToolChromeControl` | the tab strip moves into the chrome's own strip |
| `ToolControl` | so the strip is not drawn twice |
| `DockTarget`, `GlobalDockTarget` | the selectors are PNG images in Dock's theme |
| `ToolPinnedControl`, `ToolPinItemControl` | the strip has no border and the item is a plain label |
| `ProportionalStackPanelSplitter` | the seam |
| `HostWindow` | so a floated tool wears the drawn window frame |

**A tool dock is one strip.** Dock draws a title row with the dock's buttons and puts the
tool tabs along the bottom, which is two rows where the design has one. The strip in the
replaced chrome holds the tabs on the left and the buttons on the right, and `ToolControl`
draws only content. **The buttons take their width first and the tabs take what is left**, so
a narrow dock loses the end of a tab name and never a button.

**A dock holding one tool still draws its tab.** Dock hides a lone tab because its own header
carries the title, and ours does not. A lone tab drops its fill so it reads as the title it
is, which is `ToolTabStrip:singleitem ToolTabStripItem:selected`. A floated tool is the
exception: it keeps no strip while it is alone, because the window's title bar names it.

**A document tab and a tool tab are deliberately different.** A document is named by an
identifier, so its label is mono, it carries the 2px `Accent` top marker when its dock has
focus, a round `Warn` mark when it is modified and a close. A tool tab is a flat label in the
UI family, one tier quieter at `InkMuted`, and carries neither marker nor close.

**The marker is the tab's own top border**, not a separate part, which is what the design's
inset shadow is. A dock that does not own focus drops its open tab to `SurfaceRowAlt` on
`InkDisabled` and draws no marker.

**The tab list at the end of a document strip is ours.** Dock scrolls a strip that runs out
of room and offers nothing to reach a tab that has scrolled off, so the strip's `RightContent`
carries a chevron opening a `MenuFlyout` over `VisibleDockables`. Its rows bind through
`MenuFlyoutPresenter.tabList`, since a flyout's items source alone cannot say what a row runs.

**Every division between docks is the seam.** `ProportionalStackPanelSplitter` is one pixel
in layout, so the panes stay flush, and its template reaches three pixels either side with a
transparent band so there is something to catch. It goes `Accent` under the pointer and
widens to the band without moving while it is dragged, which is `GridSplitter`'s rule.

#### Drop targets

Three states, and they have to be distinguishable at a glance because they are read during a
drag.

| State | Look |
|---|---|
| available | `AccentTintLine` edge on `SurfaceDrop`, `Accent` glyph |
| under the cursor | `Accent` edge on `AccentTint` with `ShadowDropTarget` |
| the region it would claim | `DropPreview` wash with a `DropPreviewLine` edge |

**Avalonia stops tracking what a pointer is over once a drag has captured it**, so
`:pointerover` never fires on a target. Which one is hit is read from the preview Dock turns
on for that same operation instead: each selector holds a highlight whose `IsVisible` is bound
to the matching indicator's `Opacity`, and a double converts to a bool on its own. Measured
during a real drag: the left indicator at 0.5 and exactly one highlight visible.

**Dock writes `Opacity` on the preview region itself, and the value is 0.5.** So `DropPreview`
and `DropPreviewLine` are the design's ten and forty five percent doubled, which is said where
they are declared. Dock also floods the whole region with `DockTargetIndicatorBrush`, so that
key is set transparent and the wash is drawn by a child of it.

**A `[TemplatePart]` type is enforced by the XAML compiler.** Dock declares the preview
regions as `Panel`, so the fill and the edge sit on a `Border` inside each one rather than on
the part itself.

**There is no disallowed target.** The design draws a greyed square for one, and Dock hides an
operation it will not accept and leaves a target it might accept sitting at rest until the
pointer reaches it. A target that never lights is the refusal.

#### Floating and pinned

**A torn out dockable floats in `ui:KitbashHostWindow`**, which a tool's factory returns from
its `HostWindowLocator`. It follows the same choice about who draws a frame that every other
Kitbash window follows, so a person who asked the desktop for their frames gets one here too,
and a tool passes the flag in from `IWindowSettings`.

**Four window properties are written as local values rather than left to a style**, because
Dock hands the desktop the frame from a style carrying an activator and a plain style will not
beat one. Everything else, the shadow gutter, the corner and the frame template, comes from the
`chromeless` class the window sets on itself and the styles already in
`Themes/Controls/WindowChrome.axaml`.

**Where Kitbash draws the frame, the tool chrome is the title bar**: at `HeightTitleBar`, with
the title in the window title's own type, no tab strip while the tool is alone, and a close
that floods red. **Where the desktop draws it, none of that applies** and the strip stays an
ordinary 28px dock strip whose tab is what names the tool, since the window already has a
title bar of its own above it. Those four rules are scoped by `HostWindow.chromeless`.

**Every word a dock's menus say is overridden.** Dock's strings carry an access key marker,
which draws as a stray underscore because Kitbash menus do not use access keys, and several
are title case where the house style is a sentence. All eighteen are replaced by key in
`Docking.axaml`.

**A pinned tool is a label turned on its side.** The strip sits on the root tone with a seam
facing the content it collapsed out of, chosen from `DockPanel.Dock`, since Dock gives the
strip no alignment of its own.

#### What a tool has to supply

- The two style includes, and `AddKitbashDocking` for the layout store.
- A `HostWindowLocator` on its factory. **Dock builds no window without one**, so a tear out
  silently removes the dockable from the layout and shows nothing.
- `EmptyContent` on a document dock, since what a dock with nothing in it says is the tool's.
  Only its tone is set here.
- An `IconTemplate` if its tabs carry a mark. The library's is empty, because the leading slot
  is for a tool's own icon and only the tool knows it.

**A layout is per person and per machine.** `IDockLayoutStore` reads and writes one under the
application state directory, keyed by scope and view, so a tool with several views keeps one
for each. A layout that cannot be read or written is survived rather than reported, the way a
workspace that cannot be scaffolded is, since starting on the default layout is not a loss of
work.

**Dock's System.Text.Json serializer cannot write an initialised layout.** Measured against
12.1.0: a dockable's `Owner` is written as a polymorphic value, reference preservation does
not apply to one, and the writer recurses until it gives up at depth 64. A layout straight out
of `CreateLayout` writes fine and the same layout after `InitLayout` does not.
`Dock.Serializer.Newtonsoft` round trips it, so that is the package, and `IDockSerializer` is
registered through `TryAdd` so swapping back later is one line.
