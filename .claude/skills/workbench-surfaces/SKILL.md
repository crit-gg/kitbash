---
name: workbench-surfaces
description: "Workbench surface rules. The depth ramp and Surface.Level/Nests, SurfacePanel and section labels, list and tree rows, ui:Tree virtualisation, both data grids and their column model, tabs, and every floating overlay such as menus, flyouts, tooltips and popovers. Read before changing panels, lists, trees, grids, tabs or popups."
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
`DataGridCell.OnLostFocus`: a context menu opens in a popup and takes the focus with it, so
the edit would end under the open menu. The cell holds on while any menu inside it is open.
`TextBox.OnLostFocus` guards its own selection the same way.

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

