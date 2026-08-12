# The inspector

A property panel that edits any object with a schema. One label column, one value column,
categories, a filter, a revert affordance on anything that differs from the value under it,
and an editor per type rather than a text box per type.

Not a Slate stage. It sits beside the thirteen, it depends only on what is already built,
and nothing in the launcher needs any of it.

## Status

**Nothing here is built.** This file is the plan and the decisions behind it. Everything in
the Decisions section is settled and is not to be reopened without saying so.

## Source of truth

One file in the Claude Design project **Workbench**,
`8faef49e-39a8-4237-9440-04ab9cf949f4`, read through DesignSync.

| File | What it carries |
|---|---|
| `Theme Slate - Inspector.dc.html` | all thirteen sections, 223,794 chars, `truncated: false`, ends on `</html>` |

It is well under the 256 KiB cap and is read whole on demand rather than cached. Check the
`truncated` flag and the closing tag anyway, which is the rule in `README.md`.

**Read the control and never the content.** The page is full of invented recipes, machines,
items and materials. None of it specifies anything. Steel Bloom is not a data format.

The thirteen sections, in the page's own order:

1. The inspector
2. Row anatomy
3. Defaults, overrides and revert
4. Editing several objects at once
5. The editor per type
6. Arrays and nested objects
7. Stacked rows
8. Collections
9. Groups, subgroups and gated sections
10. Nested inspectors
11. Invalid values and properties that are switched off
12. Filtering, favourites and the row menu
13. Interactions worth stealing

## What this page already agrees with

Checked rather than assumed, and it is why the gap list is four items long.

**Every colour but three is already a token.** Mapped exactly:

| Design | Token |
|---|---|
| `#25262a` panel and rows | `SurfaceNest1` |
| `#1e1f22` title bar, category headers, footer | `SurfaceRoot` |
| `#161719` every field | `SurfaceWell` |
| `#232427` subgroup headers | `SurfaceControlOff` |
| `#2b2d31` row hover, nested level | `SurfaceNest2` |
| `#31343a` nested level | `SurfaceNest3` |
| `#33353a` field and header seams | `LineSeam` |
| `#2a2c30` subgroup seams | `LineRow` |
| `#3d4045` dividers, indeterminate box | `LineControl` |
| `#2c2e32` disabled field border | `LineControlOff` |
| `#4a4e55` dashed add borders, off caret | `LineWindow` and `MarkOff` |
| `#565d66` unchecked box border | `LineWell` |
| `#14293f` tag chips, mark tiles | `SurfaceDrop` |
| `#2b4a6b` tag chip and mark tile edges | `AccentTintLine` |
| `#8fbef5` references, breadcrumb, tag text | `AccentTintInk` |
| `#569eff` accent actions, checks, fill bars | `Accent` |
| `#0d1a29` ink on an accent fill | `AccentInk` |
| `#e6ebf2` panel and object titles | `InkTitle` |
| `#e3e5e9` values and modified labels | `InkPrimary` |
| `#a9aeb6` labels and carets | `InkSecondary` |
| `#7b8089` counts, units, quiet glyphs | `InkMuted` |
| `#5c6169` summaries, disabled values | `InkDisabled` |
| `#e0a94a` the unsaved dot | `Warn` |
| `#ef6a6e` the invalid mark and border | `Error` |
| `#f4878a` the invalid message | `ErrorInk` |
| `#a78bfa` curves, set marks, material tint | `Graph` |
| `#5bc8a8` reference type dots | `Data` |
| `#3c3357` material mark tile edge | `GraphLine` |
| `#9ba6b0` title bar glyphs, revert arrow | `InkRail` |

**Fifty three of the fifty four glyphs are in the set.** `Tag`, `Bolt`, `Undo`, `Shapes`,
`Maximize`, `Trash`, `Plus`, `X`, `Search`, `AlertCircle`, `InfoCircle`, `Columns`,
`DotsVerticalRounded`, `FolderOpen`, `Copy`, `ArrowToBottom`, `Link`, `Check`, `Factory`,
`File` and all three chevrons.

**The page's `open-external` is `ArrowOutUpLeftStrokeSquare`, verified rather than guessed.**
`icons/open-external.svg` was fetched from the design project and diffed against
`bx-arrow-out-up-left-stroke-square.svg` in the licensed set. Both paths are byte for byte
identical. It is already generated and already committed.

**Every row and header height is a token.** The row is 26, which is `HeightControl`. The
category header is 24, which is `HeightControlSmall`. Only the 22px field inside a row has
no token.

**Density.** The page is drawn dense. Type across it is 10, 10.5, 11, 11.5, 12 and 12.5,
which is the dense type scale. Nothing here moves the type.

## Decisions

Settled with the user. Nine questions and four structural ones.

**1. The model lives in Core.** `Kitbash.Core/Inspection` holds a framework free property
model that a host supplies, the way `Kitbash.Core/Settings/Schema` holds one for settings.
The inspector never knows what a recipe is, so the data format stays an open decision.

**2. The body is a plain panel and the inspector is its own control.** No `ui:DataGrid`, no
`ui:TreeDataGrid`, no `ui:Tree`, and none of the grid's column machinery. `GridColumns`,
`GridCells`, `GridRows` and `GridFrame` stay out of it. The design's word for a row is a
grid, and it means column alignment down the panel rather than a data grid.

**3. Undo belongs to the host.** The panel raises an edit and a revert and draws whatever
state it is handed. No history model in the library. The footer's Discard and its unsaved
count are readouts over what the host reports.

**4. Nested inspectors use `ui:Surface.Level` and `ui:Surface.Nests`**, the library depth
ramp, rather than the page's own four step cycle.

**5. Five editors ship as a seam and not a body.** The curve editor, the asset browser, the
gradient the prose names but never draws, the sub resource door and the shortcut capture
field. Each gets its stacked row, its mono summary and its popover anchor. The body belongs
to the tool that needs one, because nothing in the app has an asset, a curve or a gradient.

**6. The label column at depth is the markup, 124 then 114 then 104.** The page's prose says
132, 122, 112, 102 and contradicts its own markup. Level zero is 124 everywhere else on the
page, so the markup is self consistent and the prose is not.

**7. Three colours become tokens** rather than being bent onto the nearest existing one.

**8. The 22px field joins the density set**, taking it to twenty four keys.

**9. Tags reuse `ui:Chip` with its existing `accent` kind.**

**10. `tag` stays the glyph in both places**, the title bar lock and the row menu's Pin to
the top, as the page draws them.

**11. Per type memory is `IInspectorStore`**, beside `IGridColumnStore`.

**12. The table inside a stacked row is `ui:DataGrid` with `plain`.**

## Departures, recorded

Four, each deliberate.

**The body does not virtualise.** `README.md` says every list surface virtualises. This one
does not, by decision. A plain panel realises one row per property, and the page's own
framing is a schema with two hundred. What that buys is a control with no flattened row
model, no recycling contract and no shared column model, which is what building it as its
own thing means. If a tool later meets a schema large enough to feel it, the fix is
contained to the panel, because a row is a keyed `ControlTheme` either way.

**One nesting tone moves.** The page cycles `#232427`, `#2b2d31`, `#31343a`, `#25262a`. The
last three are `SurfaceNest2`, `SurfaceNest3` and `SurfaceNest1`, which is our ramp exactly.
Only the first nested level moves, from `SurfaceControlOff` to `SurfaceNest2`. The page's own
reason for its cycle, that a level four header never matches its grandparent and nothing gets
brighter forever, is the reason our ramp has three steps, so the intent survives.

**A nested body becomes one tone throughout.** The surfaces rule is that a panel's header
sits on the body's fill and is told apart by a hairline alone. The page draws the inline sub
resource with a `#232427` header over a `#2b2d31` body, which is two tones. Under our
nesting both take the level's fill and the header keeps only its seam.

**A tag chip is 2px taller than the page draws.** `HeightChip` is 21 dense where the page
draws 19. Pinning 19 is the pinned width trap, and the chip is in the density set, so the
page's number is not taken. Two pixels inside a 26px row is not worth a second chip type.

## The panel

Top to bottom. Every number is the page's.

| Part | Spec |
|---|---|
| Title bar | 32 tall on `SurfaceRoot` with a `LineSeam` seam under. Padding `0 6 0 10`, gap 7. Title 12.5px weight 600 `InkTitle` at .02em letterspacing. Then a spacer, then four 24px icon buttons at 14px in `InkRail` |
| Breadcrumb | 26 tall on `SurfaceNest1` with a seam under. Drawn only while drilled in. Padding `0 4`, gap 3. A 20px back button, then segments in `AccentTintInk` separated by 11px `ChevronRight` in `InkDisabled`, last segment `InkPrimary` weight 500 |
| Object header | Padding `9 10` on `SurfaceNest1` with a seam under, gap 9. A 30px mark tile at `RadiusControl` in the type's tint carrying an 18px glyph. Name 12.5px weight 600 `InkTitle`, trimmed. Second line at 4px below, gap 6: the type name 11px mono in the type colour, a 1 by 9 `LineControl` divider, then the path 11px mono `InkMuted` |
| Filter row | 31 tall on `SurfaceNest1` with a seam under. Padding `0 8`, gap 6. A 22px search well filling, then two 22px icon buttons at 13px, `Columns` and `DotsVerticalRounded` |
| Body | Scrolls. `ui:ScrollLane.Kind="Dense"`, which is what the page's `sb-thin` is |
| Footer | 27 tall on `SurfaceRoot` with the seam **above**. Padding `0 10`, gap 8 |

The four title bar buttons are back, forward, lock and the panel menu. Their glyphs are
`ChevronUp`, `ChevronDown`, `Tag` and `DotsVerticalRounded`. The lock is drawn active as a
`SurfaceDrop` fill with an `AccentTintLine` ring and an `AccentTintInk` glyph.

The filter well at rest carries a 12px `Search` in `InkMuted` and the placeholder Filter
properties at 11.5px `InkDisabled`. Filtering carries an `Accent` border, a 2px accent halo,
an `Accent` glyph, the text in `InkPrimary`, the match count 11px mono `InkMuted` and a 12px
`X`.

**The footer has four readings and only one is drawn at a time.**

| Reading | Content |
|---|---|
| Unsaved | A 6px `Warn` square at `RadiusMark`, then `2 changes not saved` 11px `InkSecondary`, a spacer, then `Discard` 11.5px weight 500 in `Accent` |
| Unsaved and overriding | A 5px `Warn` dot, `1 unsaved`, a 1 by 10 `LineSeam` divider, a 12px `Undo` in `InkRail`, `1 overriding Base Recipe`, a spacer, then `Filter` in `Accent` |
| Filtered | `17 properties hidden by the filter` 11px `InkMuted`, no action |
| Drilled in | A 12px `ChevronRight` turned 180 in `Accent`, then `Back to Steel Bloom` 11.5px weight 500 in `Accent` |

The page draws the unsaved mark as a 6px square in one footer and a 5px dot in another. The
dot is the mark the row gutter uses, so it is the one to keep and the square is a page
inconsistency.

**Never draw a zero.** Rule 2 of the status bar holds here. A panel with nothing unsaved and
nothing hidden draws an empty footer.

## The row

The four zones, and the reason it is a grid. From Row anatomy:

> A row is a grid, not a flex line, so labels and fields line up down the whole panel no
> matter what the editor is. The gutter and the trailing zone are always reserved even when
> empty, because a revert arrow appearing must not shift the field it belongs to.

```
12px      LabelWidth   1fr        22px
mark      label        editor     trailing
```

Row height is `HeightControl`. Hover fills the whole row with `SurfaceNest2`.

**The mark column carries the dot and nothing else.** `SizeMark`, centred. `Warn` for
unsaved, `Error` for invalid. Nothing otherwise. On a stacked row it top aligns at 9 so it
sits against the label line.

**The label column is `LabelWidth`, one value the panel owns.** 124 at the top level. Padding
`0 8 0 6`, 11.5px Archivo, trimmed with `ui:TextTip.Shows`. The tooltip carries the full
text plus the type plus the doc line. Its edge is draggable, which is what the one value on
the panel is for, and it is also what shrinks with depth. Every row binds to it, so no shared
column model is needed.

| State | Ink |
|---|---|
| Ordinary | `InkSecondary` |
| Modified or unsaved | `InkPrimary` at weight 500 |
| Untouched default, or disabled | `InkDisabled` |

**The editor column is padding `0 3 0 2`.** The field inside it is `HeightInspectorField` at
`RadiusControl`, `SurfaceWell` on `LineSeam`, padding `0 7`, gap 6, filling the column.

> The field fills the column so every editor in the panel is the same width, which is what
> makes a column of numbers scannable.

**The trailing column is 22 wide and holds one 20px slot, never two.** It is always reserved.

| What is true | What is drawn |
|---|---|
| Overriding | `Undo` 13px `InkRail` |
| A favourite | `Bolt` 13px `Warn` |
| Disabled by another property | `InfoCircle` 13px `InkDisabled` |
| None of those, pointer over the row | `DotsVerticalRounded` 13px `InkMuted` |

Right click reaches the same menu the kebab opens.

## Categories, subgroups and gated groups

**A category is the top level and it is sticky.** 24 tall on `SurfaceRoot` with a `LineSeam`
seam under, padding `0 8 0 4`, gap 6. A 13px caret in `InkSecondary`, the label 11px weight
600 in `InkPrimary` at .09em letterspacing, then an optional count 11px mono `InkMuted`.

> They carry a count where a count means something, like an array length, and nothing where
> it does not. Collapsed categories keep their header so the shape of the object is still
> readable.

**Sticky has no primitive in Avalonia.** `position:sticky` is what the page uses. It needs a
header drawn over the body and retargeted as the scroll moves, or a panel that pins one
child. It is a real build item and it is what makes a collapsed category still readable.

**A subgroup is a header, not a box.** 23 tall on `SurfaceControlOff` with a `LineRow` seam,
indent 16, gap 6, padding `0 8 0 16`. A 12px caret in `InkMuted`, the label 11.5px weight 500
in `InkSecondary`. Its rows indent their label to 22, so the label column still lines up with
everything above it. No stickiness.

**A subgroup is not a nested surface.** It is a header inside a category on the same fill.
Only a nested object sets `Surface.Nests` and takes the next tone. That split does not exist
on the page, which draws both at `#232427`, and it is the one place our nesting adds a
distinction rather than removing one.

**A gated group owns a switch, and the switch is in the header it controls.**

> When a whole feature is optional the switch belongs in the group header, not on a property
> above it, because a checkbox that silently controls the next five rows is the most common
> way an inspector lies to you.

The header gains a 14px checkbox at `RadiusCheck` between the caret and the label, and a
trailing mono 11px `InkDisabled` summary. Three states:

| State | Caret | Box | Label | Summary |
|---|---|---|---|---|
| Off | `ChevronRight` in `MarkOff` | 1.5px `LineWell` border on `SurfaceWell` | `InkMuted` | `off` |
| On | `ChevronDown` in `InkMuted` | `Accent` fill, 11px `Check` in `AccentInk` | `InkPrimary` | `custom` |
| Partly on | `ChevronDown` in `InkMuted` | `LineControl` fill, a 7 by 1.5 bar in `InkSecondary` | `InkPrimary` | `2 of 3` |

**Off collapses, it does not empty.** The rows come back exactly as they were, values intact.
An off group can carry one line saying what it would add, at padding `7 8 9 22` in 11.5px
`InkDisabled`, which is what makes an optional feature discoverable instead of invisible.

**A group summarises what is modified inside it**, as `2 modified`, so a collapsed group
never hides an unsaved change.

**Gated is not the same as disabled.** A gated group is a choice somebody made. A disabled
row is a consequence of another property, and it keeps its field so the value it will use is
still readable.

## Stacked rows

For an editor that cannot say anything useful in the value column. The row is the same row
with its two halves stacked, so the gutter, the revert arrow and the row menu all stay where
they were.

```
12px      1fr
mark      label line
          body
```

The label line is 24 tall, padding `0 3 0 6`, gap 6. The label, then an optional mono summary
at 11px `InkDisabled`, then a spacer, then the same 20px trailing slot. The body is padding
`0 4 7 4`.

**Stacking is a property flag, not a guess.** A table, a curve, a gradient, a sub resource
and any string marked multiline stack by default, and anything can opt in.

**Full width means full width.** The control gets the panel minus the 12px mark column and
4px of air. Nothing indents it to line up with the value column, because that is the entire
reason to stack.

**One mark per property.** A stacked row keeps its dot on the label line, so a changed table
reads as one property changing rather than one line of it.

**Summaries stand in for content.** A collapsed sub resource shows its name and type, a curve
shows `2 keys`, a table shows `3 rows`. A stacked row that is closed still answers what it
holds.

**It is also the narrow panel fallback.** Below about 320px of panel width every row stacks.

**Stacking must not become the default.**

> A panel of stacked rows is a form, and a form cannot be scanned for the two values you
> actually came to change. If an editor fits in the value column it stays there.

## The editor per type

Sixteen, plus one state. Each is the whole content of the value column, sized to it.

**Two rules keep the set coherent.** Every field is `HeightInspectorField` inside a
`HeightControl` row, so the row rhythm survives even where the editor is a grid of eight
toggles. And **no editor opens a dialog for its primary action**. A colour picker, a curve
editor and an asset browser are all popovers anchored to the field.

| Type | Editor | Built from |
|---|---|---|
| bool | A 14px box at `RadiusCheck` in a 26 wide slot at padding left 1. `Accent` fill, 11px `Check` in `AccentInk`. Not a full width field | `CheckBox`, keyed |
| int | Right aligned mono `InkPrimary` | `NumericUpDown`, keyed |
| float | The same plus a unit suffix 11px mono `InkDisabled` | `NumericUpDown`, keyed |
| ranged float | The same plus an absolute fill from the left at `rgba(86,158,255,.20)` sized to the fraction | `NumericUpDown`, keyed |
| string | Left aligned **Archivo** `InkPrimary` | `TextBox`, keyed |
| multiline | 46 tall inside a 56 tall row, padding `5 7`, text 11.5px at 1.4 leading, top aligned. Stacked in the panel at min height 44 and 1.45 leading | `TextBox.notes`, keyed |
| enum | Archivo value, trailing 13px `ChevronDown` in `InkMuted` | `ComboBox`, keyed |
| flags | Eight 19px squares at `RadiusControl`, gap 2. On is `Accent` fill and border with 10px weight 600 mono in `AccentInk`. Off is `SurfaceWell` on `LineSeam` with `InkDisabled` | `ItemsControl` of `ToggleButton`, keyed |
| vec3 | Three equal fields in a 4px gap grid. Each padding left 5, an axis letter 10.5px weight 600 mono in its axis colour, a spacer, then the value right aligned | new `ui:VectorField` |
| colour | Padding left 3, a 16 by 14 swatch at `RadiusMark`, hex mono `InkPrimary`, a spacer, alpha percent mono `InkMuted` | `ui:ColorField`, keyed |
| reference | A 7px square mark at `RadiusMark` in the type colour, id mono in `AccentTintInk`, a spacer, 12px `ArrowOutUpLeftStrokeSquare` in `InkMuted` | new, over a host supplied picker |
| asset | Padding left 3, a 16px thumbnail at `RadiusMark` on `SurfaceNest2` with a `LineControl` edge, name Archivo `InkPrimary`, a spacer, 12px `Search` and 12px `X` | seam only |
| path | Mono `InkSecondary`, a spacer, 12px `FolderOpen` in `InkMuted` | `ui:PathField` with `IsCompact` |
| tags | `ui:Chip` with `accent`, gap 4, then `ChipAddButton` | already built |
| curve | Padding `0 5`, a baseline hairline at bottom 5 in `LineControlOff`, the curve in `Graph` | seam only |
| shortcut | Mono `InkSecondary`, a spacer, 12px `X` | seam only |

**`mixed` is a state, not a type.** A field whose value disagrees across the selection draws
the word `mixed` at 11.5px Archivo `InkMuted` in italic.

> A dash reads as a value, and in a grid of numbers an em dash reads as zero.

The stacked curve is 64 tall with grid lines at 21 and 42 and at 33 and 66 percent in
`SurfaceControlOff`, the curve in `Graph`, and two 7px round handles in `Graph`. At depth it
is 40 tall with no grid.

## Collections

Three kinds, and telling them apart at a glance matters more than saving space.

**The header owns the collection.** 24 tall on `SurfaceRoot`, padding `0 6 0 4`, gap 6. The
caret, the name 11px weight 600 at .09em, the count 11px mono `InkMuted`, a spacer, then a
20px `Plus` at 13px in `InkRail` and a 20px `Trash` at 13px in `InkMuted`. Add, clear and
collapse live here and are never repeated per row.

**The count says which kind it is.** `3 elements`, `4 members`, `3 entries`.

**The footer strip is 24 tall on `SurfaceRoot` with the seam above.** A mono 11.5px weight
500 `InkMuted` kind word and an 11px Archivo `InkMuted` note. `array` and `order is data`.
`map` and `sorted by key`.

| Kind | Row | Rule |
|---|---|---|
| Array | `12 / 26 / 1fr / 22`. The 26 holds a 12px `DotsVerticalRounded` grip in `InkDisabled` and the index 11px mono `InkMuted`, gap 5, padding left 5. Trailing is a 12px `X` | Ordered, so it has indices and a grip. Duplicates are allowed |
| Set | `12 / 1fr / 22`. Field padding `0 2 0 6`. No index, no grip | Not ordered and cannot repeat. Adding a duplicate is a non event rather than an error |
| Map | `12 / 1fr / 22`, and the value column is an inner grid of `minmax(0,116) / 10 / 1fr`. Key, an 11px `ChevronRight` in `InkDisabled`, value | Two editors per row. The only invalid state it reaches is a repeated key |

**The index is the label.** Mono, muted, in the label column. It is not a name, so it does not
get name styling.

**The grip is the drag target and it only lights on row hover.** Dropping shows the same
dashed accent line the docking targets use.

**A set picker greys what is already in it.** The dropdown draws an existing member on
`SurfaceControlOff` in `InkMuted` with `already in` at 11px `InkMuted` on the right. Never
offered as a second copy.

**A duplicate key is marked on the row that repeats, not on the original.** An `Error` dot in
the gutter, an `Error` border and `ErrorInk` text on the key field, and an error row under it
reading `tier_2 is already a key. The later entry is ignored until you change it.`

**Add is a dashed pill.** `HeightInspectorField` at `RadiusControl`, a 1px dashed `LineWindow`
edge, 11.5px weight 500 `InkSecondary`, a 12px `Plus`. Inline in a category it spans columns
2 to 4. In a collection panel it sits at padding `5 6 7`.

**Empty is a row, not a hole.** An empty collection shows one muted line saying there is
nothing yet, with the add action under it. A collection that vanishes when emptied cannot be
refilled.

Six more rules, all from the page:

- Click an element row to select it, shift click for a run. Delete removes the selection in
  one step and one undo.
- Alt with up and down moves the selected element without the pointer.
- A map keyed by an enum gets a dropdown in the key field and can only reach the keys it has
  not used. A string key gets a text field and the duplicate check.
- Copying a collection and pasting it onto another property of the same type replaces the
  whole thing. Pasting a single element onto a row inserts after it.

## Nesting

Four levels open in place. Level five is a door.

**Our depth ramp carries the surface.** `ui:Surface.Nests` on the nesting element, which reads
its parent level and holds one below. A nested child moved to another depth is right without
being told.

**The rail carries the depth for the rows.** One hairline per level at 10px steps, at x 9, 19,
29 and 39. It is quieter than a seam because it is a guide and not a boundary.

> One hairline per level at 10px steps, so four levels cost 40px of gutter instead of the
> 64px that a comfortable indent would want.

**Header indents are 16, 26, 36, 44.** Row labels under them indent to 26 and 36. Nothing in
the panel ever indents past 44.

**The label column shrinks with depth: 124, 114, 104.** The editor keeps its width, the labels
give up ten pixels a level, and the fields still line up down the whole panel.

**Level three onwards stacks**, which is what buys levels three and four.

> Below two levels a side by side row cannot hold both a name and a useful editor, so the
> child stacks and the editor takes the full remaining width.

**Level five is a door.** The caret becomes `Maximize` in `Accent` and the label goes
`AccentTintInk`. The child opens as its own inspector with a breadcrumb.

> The expand button and the open button are deliberately different glyphs in the same place.
> A caret means the child will appear below without losing anything, and the frame glyph
> means the panel will be replaced and a breadcrumb will appear. Nothing else in the app uses
> one to mean the other.

Six rules about drilling in:

- **The breadcrumb is the truth about ownership.** Every step is clickable, the last one is
  the current object, and a long trail collapses in the middle rather than at either end.
- **Owned or shared, said plainly.** The header says `owned by this recipe` for a sub resource
  belonging to the parent, or `shared by 4 objects` for one that does not.
- **Drilling in does not change the selection.** The panel is browsing, not selecting.
- **History is a browser history.** Back and forward in the title bar, plus the mouse back
  button. Opening a child from a different parent starts a new trail.
- **The child owns its own defaults.** A nested object has its own revert marks against its
  own type defaults, and reverting the parent property clears the whole child rather than
  each field in turn.

## Defaults, overrides and revert

Two facts, and they use the two ends of the row.

| State | Mark | Meaning |
|---|---|---|
| Unsaved | A `SizeMark` `Warn` dot in the gutter | The value on screen differs from the value on disk. It clears the moment the object is saved |
| Overriding | `Undo` in the trailing slot | This object states the property for itself instead of taking it from the base |
| Inherited or default | Nothing | The label tone says which. `InkSecondary` for inherited, `InkDisabled` for an untouched default |
| Both | Both, at opposite ends | Saving clears the dot and leaves the arrow, which is right. The change is committed and the override is still there |

**There is no arrow on an unsaved row**, because undoing an edit is what undo is for, and the
footer discards the lot. **There is no second mark on an overriding row**, because the only
row that can be reverted is one that overrides something.

**Undo and revert are different actions and the panel keeps them apart.**

> Undo walks back the edits you made, in order, with Ctrl Z, and the status bar discards all
> of them at once. Revert is not about time at all, it drops an override so the property
> follows the base again, and it is a lasting change that itself goes on the undo stack.

Clicking a dot names its source and prints the value behind it.

## Editing several objects at once

The panel shows the properties they have in common.

- **The word, not a dash.** A field that disagrees says `mixed` in italic secondary text.
- **Typing commits to all.** Entering a value replaces it on every selected object and the
  field stops being mixed. Nothing is written until you commit, so tabbing through a mixed
  field leaves it alone.
- **Booleans get the third state.** The indeterminate tick is the same bar a partly checked
  tree row uses. Clicking it turns everything on, clicking again turns everything off.
- **Only the common schema.** Properties that do not exist on all of them are hidden, with a
  count in the panel menu so what is left out is visible.

The header becomes the mark tile, `3 recipes selected` and `Editing what they have in common`
at 11px Archivo `InkMuted`.

## Invalid values and properties that are switched off

> A value can be wrong, and a value can be irrelevant. They look nothing alike on purpose.

**Wrong keeps its field.** The field takes an `Error` border and a 2px `rgba(239,106,110,.25)`
halo, the gutter takes an `Error` dot, and the message takes a row of its own: the gutter dot
again, then a span across columns 2 to 4 at padding `0 10 7 6`, gap 7, a 13px `AlertCircle` in
`Error` and the text 11.5px at 1.4 leading in `ErrorInk`.

> It pushes the rest of the panel down, which is correct here: an error you can scroll past
> without noticing is worse than a panel that moved.

**Irrelevant flattens and says which property is holding it.** The field goes to the disabled
well fill with a `LineControlOff` border, the value to `InkDisabled`, the unit to `MarkOff`,
the label to `InkDisabled`, and the trailing slot takes `InfoCircle` at 13px `InkDisabled`.
**Nothing uses opacity**, so a disabled row still reads as a row and the panel does not
develop holes.

**The condition is named.** The tooltip is a 270 wide card on `SurfaceRoot` with a
`LineControl` edge at `RadiusSurface` and `ShadowPopup`, padding `9 11`, gap 8, a 13px
`InfoCircle` in `AccentTintInk` and text 11.5px at 1.45 leading in `InkPrimary`. The named
property is a link that scrolls to it and flashes its row.

## Filter, favourites and the row menu

**Filtering matches the label, the type and the property path**, so typing a type name lists
every float on the object. Categories with no match collapse away rather than showing empty.

- **Matches are marked, not filtered out of the label.** The matched run carries
  `rgba(86,158,255,.22)` with `InkPrimary` text.
- **The count is in the field**, on the right of the search box, and the footer says how many
  rows the filter is hiding.

> A panel that silently shows four of two hundred properties is a support ticket.

**Favourites survive filtering.** A property starred here is starred for every object of that
type, and it sits in a pinned FAVOURITES category at the top of the panel.

**The row menu is the same list everywhere.** A 270 wide popover on `SurfaceRoot` with a
`LineControl` edge at `RadiusSurface`, padding 5, `ShadowPopup`. Items 26 tall at padding
`0 10` and `RadiusControl`, gap 9, a 14px glyph in `InkRail`, the label 11.5px `InkPrimary`, a
spacer, then the gesture 10.5px mono `InkDisabled`. Separators are 1px `LineSeam` at margin
`5 7`.

| Group | Item | Glyph | Gesture |
|---|---|---|---|
| 1 | Revert to inherited value | `Undo` | |
| 1 | Copy value | `Copy` | Ctrl C |
| 1 | Paste value | `ArrowToBottom` | Ctrl V |
| 2 | Add to favourites | `Bolt` | |
| 2 | Pin to the top | `Tag` | |
| 3 | Copy property path | `Link` | |
| 3 | Copy as code | `CodeAlt` | |
| 4 | Show documentation | `InfoCircle` | F1 |

**Copy and paste work across objects of different types as long as the type matches**, which
is how a tuning pass gets done.

It is a `ContextMenu` and not a `MenuFlyout`, so it opens at the cursor at its own width. See
the `kitbash-surfaces` skill for why the two are not interchangeable.

## Interactions

Eight. None is visible in a screenshot, which is why they get dropped.

1. **Drag the label to scrub.** Dragging left or right on a numeric label changes the value
   without opening the field. Shift for fine, Ctrl for coarse, and the cursor turns into the
   horizontal resize arrows while it is live. `ui:Scrub` already does this on `NumericUpDown`
   and needs extending to the label.
2. **Type a sum in a number field.** `14/2` and `6+0.5` both commit as numbers.
3. **Drag an asset onto a field.** Dragging a compatible asset highlights the fields that
   accept it and dims the ones that do not.
4. **Middle click a label to revert.**
5. **Escape cancels, Enter commits.** Every field. Escape puts back the value the field opened
   with and never leaves the row half edited.
6. **Alt click a category caret** expands or collapses it and all of its children.
7. **The panel remembers per type.** Collapsed categories, the label column width and the
   filter.
8. **Lock the panel.** The lock in the title bar stops the panel following the selection.

## What has to be added

**Three colours.** They go in `Tokens.axaml` with everything else.

| Key | Value | For |
|---|---|---|
| `LineNest` | `#2f3237` | the nesting rail. Quieter than `LineSeam` and a rung above `LineRow` |
| `SurfaceWellDisabled` | `#1b1c1f` | a disabled field. `SurfaceWellOff` is `#1e1f22` and is the read only well, which is a different thing |
| `AxisX` | `#d97b7b` | the X channel |
| `AxisY` | `#7fc08a` | the Y channel |
| `AxisZ` | `#6ea8e8` | the Z channel |

`AxisX` and `AxisZ` hold the same values as `PinBool` and `PinInt`. They are separate keys
because the two uses would move apart: a pin colour is a data type and an axis colour is a
spatial convention. `theme-inventory.md`'s rule is to collapse a repeat unless the two uses
would ever separate, and these would.

**One size, and it joins the density set.**

| Key | Dense | Comfortable |
|---|---|---|
| `HeightInspectorField` | 22 | 27 |

The comfortable value is derived rather than read, since the page draws one density. 27 keeps
the 2px of air above and below the field that 22 gives it inside a 26px row, which is the
mechanism behind the page's own reason for the number. **Leaving it out of
`KitbashComfortable.axaml` is the documented failure**: a key that file does not name keeps
its dense value, so the field would stay 22 inside a 31px row while everything around it grew.
The set goes from twenty three keys to twenty four, and the `kitbash-controls` skill's table
has to be amended with it.

**One icon.** `code-alt`, for Copy as code. `bx-code-alt.svg` is present in the licensed set
at `/home/jason/Seafile/gamedev-assets/icons/box-icons-pro-solid-rounded/`. Add the name to
`tools/icons/icons.txt` and rerun `tools/icons/generate.py`. Never hand edit `Icons.axaml` or
`IconGlyph.cs`.

## The contract

`Kitbash.Core/Inspection`, framework free, taking nothing beyond what Core already has.

What a property carries: its key and path, its display name, its doc line, its type, its
value, its unit, its range, its choices where it has them, whether it stacks, and which
category and group it belongs to. What state it is in: default, inherited, overriding,
unsaved, invalid, disabled, or mixed. What its row menu can do.

What an object carries: its display name, its type name, its type colour and glyph, its path,
whether a nested one is owned or shared, and its categories and groups in order.

**The host supplies all of it.** Nothing here reads a file, names a format or knows what a
recipe is. That keeps the three open decisions in the root `CLAUDE.md` open.

**The host also owns undo.** The panel raises an edit and a revert. It draws whatever state
comes back. The footer's counts and its Discard are readouts.

## The controls

| File | What |
|---|---|
| `Kitbash.Core/Inspection/` | the property and object model, framework free |
| `Kitbash.Ui/Controls/Inspector.cs` | the panel. Chrome as template parts, each hidden when nothing supplies it |
| `Kitbash.Ui/Controls/InspectorRow.cs` | the four zone row. One `Grid`, three fixed zones and one that moves |
| `Kitbash.Ui/Controls/InspectorStackedRow.cs` | the two zone row |
| `Kitbash.Ui/Controls/VectorField.cs` | three fields holding one value |
| `Kitbash.Ui/Controls/IInspectorStore.cs` | per type memory |
| `Kitbash.Ui/Themes/Controls/Inspector.axaml` | the row grid, the headers and the chrome |
| `Kitbash.Ui/Themes/Controls/InspectorForms.axaml` | the sixteen editors as keyed themes, beside `GridCellForms.axaml` |
| `Kitbash.Gallery/Views/Pages/InspectorPage.axaml` | the eleventh page |

**The editors are keyed `ControlTheme`s and not new controls**, which is the precedent
`GridCellForms.axaml` set. Eight of the sixteen are the same eight controls the grid already
re keys, at `HeightInspectorField` instead of a cell.

**`IInspectorStore` is the third of its shape.** `IGridColumnStore` keeps a grid's columns and
`IDockLayoutStore` keeps a dock layout. Both take a `SettingsScope` plus a key, both live in
application state rather than in a workspace, and both survive a machine that will not take
the write by starting on the declared state next time. This one keeps, per object type, which
categories are collapsed, the label column width, the filter text and the favourited property
paths.

**Nothing hosts the inspector**, since no tool exists, so the gallery is where it runs and
where it is verified. Same as `ui:ProjectsWindow`.

## Phases

Each depends only on the ones before it. Phases 1 to 4 are the panel. Everything after 4 is
additive and can be reordered or dropped.

| # | Phase | What lands |
|---|---|---|
| 1 | Contract and tokens | `Kitbash.Core/Inspection`, the five colours, `HeightInspectorField` in both density files, `code-alt`. No UI |
| 2 | The row and the panel | The four zone row, the panel chrome, the sticky category header, the footer's four readings. Five editors: bool, int, float, string, enum |
| 3 | Defaults and revert | The two marks, the trailing slot rules, the row menu, the footer counts, the edit and revert events |
| 4 | Stacked rows and groups | Stacking as a flag, subgroups, gated groups and their three header states, the narrow panel fallback |
| 5 | Collections | Array, set and map, the header and footer strips, row selection, reorder by grip and by Alt arrows, duplicate keys, the empty row |
| 6 | Nesting | `Surface.Nests`, the rail, the shrinking label column, stacking from level three, level five as a door, the breadcrumb and the history |
| 7 | The rest of the editors | Ranged float, vec3, flags, colour, path, tags, reference, multiline. Seams for asset, curve, shortcut, gradient and sub resource |
| 8 | Several objects at once | The common schema, `mixed`, the third checkbox state, commit to all |
| 9 | Filter and memory | Match marking, the count, favourites, the pinned group, `IInspectorStore`, the label column drag |
| 10 | Invalid and disabled | The error row, the flattened field, the named condition tooltip and its scroll and flash link |
| 11 | Interactions and finish | The eight behaviours, the keyboard contract, automation peers, the gallery page, headless verification |

## Not built

**The five seams.** The curve editor, the asset browser, the gradient, the sub resource body
and the shortcut capture field. Each gets its row, its summary and its popover anchor and
nothing else. Nothing in the app has an asset, a curve or a gradient, so a body built here
would be built against nothing.

**Nothing in the design is refused.** Everything above is either planned or a recorded seam.
