# Stage 7: panels and splitters

The surfaces a tool is assembled from, and the depth rule that keeps them readable.

**Built.** What follows is the plan as written, with the departures recorded at the end.
`.claude/CLAUDE.md` under Panels and the depth ramp is what the code actually does.

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
stage 12 and the answer has to survive that.

## Build

**Built in types.** A panel is a header over content, which is what
`HeaderedContentControl` already is, and a collapsible one is an `Expander`, which is a
`HeaderedContentControl` itself. A splitter is a `GridSplitter`, which is a `Thumb` and
already carries the drag. A sidebar that slides away is a `SplitView`.

| Thing | Type |
|---|---|
| panel | `HeaderedContentControl`, plus a `Footer` of ours |
| collapsible panel | `Expander` |
| splitter | `GridSplitter` |
| collapsing sidebar | `SplitView` |

The only new member in the list is the footer, because no built in type carries a
header, a body and a footer at once. Add that and nothing else.

**Panel.** A header, a content area and an optional footer. Header carries a title at
12.5px weight 600 `InkTitle`, optional actions on the right, and a `LineSeam` under it.
Footer sits on `SurfaceRoot` as chrome does. 8px radius on the panel shell.

A dialog already has this shape, so reuse `DialogFooter` from stage 3 rather than
building a second footer, or rename it if a panel footer and a dialog footer turn out to
differ.

**Panel header actions.** Icon buttons at the 24 by 24 minimum, `InkSecondary`, going
to `InkPrimary` on hover. That is the stage 4 icon kind with no new styling.

**Splitter.** A draggable seam between panels, themed from `GridSplitter`. It reads as a
`LineSeam` at rest, widens its hit area beyond its visual width, and takes `Accent`
while dragging. The hit area is the part that gets forgotten. A one pixel line is not
draggable, so it needs a transparent grab margin, and it needs a `Background` or it will
not be hit tested at all.

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

All four hold, measured headless against the library rather than looked at. Five deep
reads 1, 2, 3, 1, 2 with each tone's own seam. A button four steps down is `#2b2d31` with
a `#33353a` border, the same as at the top. The splitter measures one pixel of layout with
seven of bounds in a row and in a column alike. A panel moved between two grounds repaints
itself and its children, and so does a panel whose ground moves underneath it while it is
told nothing.

## Where this departed from the plan

Each of these came from reading the design again rather than from preference.

**A panel's footer is part of the panel, not chrome.** The plan says the footer sits on
`SurfaceRoot` and to reuse `DialogFooter`. The design's prose is explicit the other way:
"Within a panel there is no second fill: header, body and footer all sit on the panel
tone, separated from each other only by the hairline." The one drawn panel with a
`#1e1f22` header and footer is the outermost frame, whose header is a toolbar and whose
footer is a status bar, and the inventory lists both of those under `SurfaceRoot` already.
So the footer is a member of `SurfacePanel`, and `DialogFooter` is left alone: it aligns
right and sits on the root tone, which a panel footer does neither of.

**A tone's seam comes from the tone itself, not from what it sits on.** The plan says the
border depends on the tone the element sits on. The depth ladder and all six panels in the
deep nesting example say otherwise: a `#2b2d31` panel draws `#3d4045` wherever it is. The
design's own words for it are "each with its own seam". Controls are the case the plan's
phrasing came from, and they are off the ramp entirely.

**The panel title is 11.5px medium `InkSecondary`.** The inventory's type table says 12.5px
600 `InkTitle` for a panel title. Nothing on the theme page draws one: both 12.5px uses in
that document are the window title, active and inactive. Every drawn panel header is 11.5px
weight 500 `InkSecondary`.

**The empty panel's mark is an icon.** The design draws a 22px dashed square, which is what
it draws everywhere a glyph belongs, and the inventory already records that the page shows
position and size rather than the glyph. So the view names one from the set instead, at the
large size empty states take.

**One thing in the design is contradicted by the design.** The section intro says "A panel
is one tone throughout, and it gets a fill or a border, never both", and the written spec
says hierarchy is never carried "by stacking fills and borders on the same element". Every
panel drawn on the page has both. Built as drawn, the way the chip's pressed border was.

**`SplitView` is themed for all four placements and all four display modes**, though the
design draws no specimen. Every length is one the control computes and publishes through
`TemplateSettings`, so nothing is invented here. Verified across all sixteen combinations.

**The section label is a keyed theme over `HeaderedContentControl`.** The plan lists it as
a style. A label with a note at the end is a header over content, which is a type Avalonia
already ships, so it is themed rather than built.
