# Stage 9: lists and trees

Row surfaces. The first stage where virtualisation and container recycling matter.

## Goal

A tree and a list that stay correct at ten thousand rows and whose row states never
stack.

## Build

**List row.** 26px, 2px between rows in a list surface, 5px radius on the row itself.
Text at 12px `InkPrimary`.

**Tree.** 25px rows, 14px indent per level, a `#2a2c30` indent guide per level, and a
disclosure chevron that rotates rather than swapping glyph. Rotate through
`RotateTransform.Angle`.

**Row states.** Seven, and they compose in a defined order rather than stacking:

| State | Look |
|---|---|
| normal | no fill |
| hover | `StateHover` |
| selected | `AccentTint` fill, `SelectionInk` text |
| selected and focused | the selection tint plus a low alpha inset accent line |
| modified | a `#e0a94a` mark, the fill unchanged |
| drop target | an `Accent` outline |
| disabled | `InkDisabled` text, shape kept, no opacity |

The rule that matters: **a row never carries both a fill and an outline**, and selection
is the tint alone with no left marker. Focus on a selected row is an inset line rather
than the usual halo, precisely so it survives the tint. Getting this wrong produces a
row with a tint, a border and a halo all at once, which is what the spec is written to
prevent.

## Virtualisation

Every list surface virtualises. That is a standing rule for this library, not a
per control decision, and it is the reason this stage is larger than it looks.

**What Avalonia gives away free, and what it does not.** Checked against the 12.1.1
source rather than assumed:

- `ListBox` overrides its panel to `VirtualizingStackPanel`, so a flat list virtualises
  with no work.
- `ItemsControl` defaults to a plain `StackPanel`. Anything built on it realises every
  item unless the panel is replaced.
- `TreeView` does not override the panel either, so it inherits that same
  `StackPanel`, and so does every `TreeViewItem` for its children. **A tree does not
  virtualise in Avalonia 12 and there is no virtualising tree panel in the box.**

So the tree needs the standard answer: flatten the expanded nodes into a flat list of
visible rows, virtualise that list, and let expand and collapse edit the flat list
rather than the visual tree. Depth becomes a value on the row rather than a nesting
level, which also makes the 14px indent and the indent guides a straight calculation.

Do this first. It decides the shape of everything else in the stage, and retrofitting it
onto a nested tree means writing the tree twice.

**Recycling.** Containers are reused. Anything set when a container is prepared has to
be reset in the same place, never only on first use, or a scrolled row will show another
row's state. Avalonia 12 exposes this through `PreparingContainer`, `ContainerPrepared`,
`ContainerIndexChanged` and `ContainerClearing`. `ContainerIndexChanged` fires when a
recycled container is reused at a different index and is the one usually missed.

A custom virtualising panel derives from `VirtualizingPanel` and must implement
`GetControl` so keyboard navigation still works. A panel that scrolls correctly but
cannot be arrowed through is not finished.

Selection in Avalonia 12 changed: touch and pen select on release rather than press,
`UpdateSelection` and `UpdateSelectionFromEventSource` are obsolete, and the overrides
are now `ShouldTriggerSelection` and `UpdateSelectionFromEvent`. Any custom selection
behaviour uses the new ones.

## Tree data grid

Hierarchy in the first column, aggregates on the branch rows. The branch row shows
rolled up values in the same columns its children use, in monospace, at `InkSecondary`
so it reads as a summary rather than as data.

Hand built, like everything else here. TreeDataGrid is licensed and is not an option.

Build the tree first and the columns second. A tree data grid is a tree that also lays
out columns, so if the stage 9 tree is right, this is column layout on top of it rather
than a separate control. Share that column layout with stage 10 rather than writing it
twice.

## Done when

- A tree of ten thousand nodes scrolls without allocation churn and no row shows
  another row's state.
- All seven row states render and none of them stack a fill with an outline.
- Focus is visible on a selected row.
- Keyboard navigation moves through the tree and expands and collapses.
