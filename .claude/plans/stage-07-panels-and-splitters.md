# Stage 7: panels and splitters

The surfaces a tool is assembled from, and the depth rule that keeps them readable.

## Goal

A panel that can be nested to any depth and never becomes ambiguous about where one
surface ends and the next begins.

## The depth rule

This is the part of Slate most likely to be implemented wrong, because it is not what
most dark themes do.

`SurfaceRoot` `#1e1f22` is the root and is unique. It never appears again inside the
window. Everything nested below it cycles three tones and starts over:

```
root      #1e1f22
  level 1 #25262a
    level 2 #2b2d31
      level 3 #31343a
        level 4 #25262a   back to the first
```

Nothing lightens progressively with depth, and a grandchild never matches its
grandparent. Controls do not ride this ramp at all: input wells stay `SurfaceWell` and
buttons stay `SurfaceNest2` no matter how deep they sit.

The border a control draws depends on which tone it sits on: `LineSeam` on root or
level 1, `LineControl` on level 2, `LineControlDeep` on level 3.

Implementation options, in order of preference:

1. An attached property carrying the depth, set once by the panel container and read by
   the children through a converter. A panel does not need to know its own depth, only
   to add one to its parent's.
2. Explicit per level control themes selected by the author. Simpler, but every author
   has to count, and they will get it wrong.

Do not compute it from the visual tree at runtime. Panels get reparented by docking in
stage 11 and the answer has to survive that.

## Build

**Panel.** A header, a content area and an optional footer. Header carries a title at
12.5px weight 600 `InkTitle`, optional actions on the right, and a `LineSeam` under it.
Footer sits on `SurfaceRoot` as chrome does. 8px radius on the panel shell.

**Panel header actions.** Icon buttons at the 24 by 24 minimum, `InkSecondary`, going
to `InkPrimary` on hover.

**Splitter.** A draggable seam between panels. It reads as a `LineSeam` at rest, widens
its hit area beyond its visual width, and takes `Accent` while dragging. The hit area is
the part that gets forgotten. A one pixel line is not draggable, so the control needs a
transparent grab margin, and it needs a `Background` or it will not be hit tested at
all.

**Empty panel.** A centred message at `InkSecondary` and a single action. The design
shows `No graph assigned` with an `Assign` button. Build the pattern, not that content.

**Section label.** The small capitalised label used above groups: 11px weight 600,
`InkMuted`, letter spacing `.1em` to `.12em`, followed by a `LineSeam` rule that fills
the remaining width. Used in the launcher already and in every panel here.

## Done when

- A harness nests panels five deep and the tones cycle correctly with no two adjacent
  levels matching.
- A control at depth four still draws on `SurfaceWell` or `SurfaceNest2`, proving
  controls are off the ramp.
- The splitter can be grabbed reliably at its visual width plus a margin.
- Reparenting a panel updates its depth without a relayout hack.
