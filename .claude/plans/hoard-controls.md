# Controls the asset tool needs

Thirteen controls `Kitbash.Ui` does not have, taken from two design pages. Not a Slate
stage. It sits beside the thirteen, everything here depends only on what is already built,
and nothing in the launcher needs any of it.

## Status

**Nothing here is built.** The alternative styles those same two pages needed are built and
are in the `kitbash-controls` and `kitbash-surfaces` skills. This page is the other half:
what has no type to theme.

## Source of truth

Two files in the Claude Design project `8faef49e-39a8-4237-9440-04ab9cf949f4`, read through
DesignSync. Both read whole and both end on `</html>`.

| File | What it carries |
|---|---|
| `Hoard.dc.html` | the browser window, its sidebar, both views and the inspector |
| `Hoard Asset Window.dc.html` | one asset in its own window, and the five preview kinds |

**Read the control and never the content.** Both pages are full of invented assets, owners
and folders. None of it is a specification for anything.

## What these pages already agree with

Checked rather than assumed, and it is why this list is only thirteen long.

- **Every icon is in the set.** All twenty two, `IconGlyph` already names each one. No
  generation, no addition to `tools/icons/icons.txt`.
- **Every colour is a token.** The depth ramp matches exactly: the pages' chip, control and
  raised fills are `SurfaceNest1`, `SurfaceNest2` and `SurfaceNest3`. The kind colours are
  the pin family, the statuses are `Ok`, `Warn` and `InkMuted`, and the accent and tint
  families are ours. Three colours were added, all for one callout, and they are
  `WarnControl`, `WarnControlHover` and `WarnControlInk`.

## Density, settled

**These pages are drawn at the second density, and both densities now exist.** Dense is the
default and is the spec's own number. An app that browses rather than edits takes
`Themes/KitbashComfortable.axaml` as a second style include, which is twenty one geometry
keys over the top. The set, the rule that density is geometry and never type, and the pinned
width trap are all in the `kitbash-controls` skill.

So an asset tool built from these pages takes that line, and every number below is already
right without anything here pinning one.

**Type does not move with it.** Measured across both pages: 11, 11.5, 12, 12.5 and 13, which
is the dense type scale exactly. A roomier tool is not a tool whose words are bigger.

`SizeMark` going from 5 to 7 is part of that set, which is what makes the chip's round and
square marks tell apart at all. At 5 they are nearly the same shape.

## The controls

Ordered by what each costs.

### 1. A virtualising tile panel

`repeat(auto-fill, minmax(N,1fr))` with a live slider driving N, gaps of 26 and 20.

**This is the largest item on the page and it is the only one with no way around it.**
Avalonia ships no virtualising wrap or uniform panel. `WrapPanel` realises every child, and
the build plan's own rule is that every list surface virtualises, so a grid of a few thousand
thumbnails breaks it.

It is the machinery `variable-height-list.md` describes, except uniform and two dimensional,
which makes it **easier** rather than harder: with one cell size the extent is arithmetic
rather than an estimate, which is the whole of what goes wrong in that page's measurements.
Read it first anyway, for `VirtualizingPanel`'s surface and the `ILogicalScrollable` part.

**Two hazards from that page apply unchanged.** An `ItemsPanel` setter is accepted and
ignored, so how a list is given the panel has to be settled first. And keyboard navigation is
part of the contract: `GetControl` is abstract, and a grid that scrolls correctly and cannot
be arrowed through is not finished. Add one this panel has of its own: the column count
changes with the width, so `GetControl` has to move by a row that is only known at layout.

### 2. A tool strip

The bar under a preview stage, and the bar over a list of results. **They are one control.**
Five different strips are drawn on the asset page, one per asset kind, and all five are built
from three pieces: a labelled button, a toggle, and a divider or a spacer, with a monospace
readout at the end. That is what makes it a control rather than a layout somebody writes five
times.

Everything it holds already exists after the alternative styles: `ToggleButton` with `.ghost`
and `.icon`, `Button.ghost`, `Slider`, and a mono `TextBlock`. The strip is the arranging and
the spacing, and it is what stops five toolbars drifting apart.

### 3. A media frame

The preview box, at three sizes: a 1:1 tile, a 4:3 inspector panel and a full stage. A well,
a radius, a checkerboard behind anything with alpha, and a selection ring.

**The checkerboard already exists and is private.** `ColorPicker.cs` builds it as a tiled
`DrawingBrush` from `CheckerLight` and `CheckerDark`. Lift it out rather than writing a
second one, and leave the picker using the shared one.

### 4. A tile card

A media frame, a top left selection check, a top right badge on a scrim, then a name row with
a trailing monospace meta. The badge is `ui:Badge` with `.onMedia`, which is built.

### 5. A waveform view

Bars, a time ruler with ticks and monospace labels, a tinted loop region with accent edges,
and a playhead with a diamond handle. No way to fake it and no library for it.

### 6. A code view

Line numbers, one colour per line, one highlighted line, read only. **Not a text editor.** It
draws a fixed list of coloured lines and takes no input, which is the whole reason it is
cheap. `ui:ScrollLane.Kind` is `Well` here.

### 7. A breadcrumb

Path segments with chevron separators, the last one strong, the rest clickable and quieter,
wrapping when the path is long.

### 8. A fact row

`label ————— value`. Muted label, a hairline filling the middle, a monospace value, one row
height. **Smallest thing here and the most used**: five separate blocks across the two pages,
about twenty one rows.

`Border.seam` is the hairline, which the `SectionLabel` theme already uses.

### 9. A status strip

The row along the foot: counts on the left, text actions on the right. `Themes/Controls/StatusBar.axaml`
is the launcher's git strip specifically, with branch, conflict and update readouts written
into it. This is the general one, and the eight rules in the `kitbash-controls` skill still
govern it, rule 2 especially: a zero is never drawn.

### 10. A step walker

`[<] [>] 4 of 38`. Two icon buttons and a monospace position. Trivial, and worth naming so
the two buttons and the readout cannot drift apart between tools.

### 11. A transport bar

A play button, a thin progress track and a monospace time. The track is `ProgressBar`, which
is themed, so this is the arrangement plus whatever seam a host uses to drive it.

### 12. A type specimen

A size label and a sample line, repeated down descending sizes. The one control here that
needs a font loaded from a path rather than from the theme, which is a platform question and
not a control one.

### 13. A dot readout

A small dot and a muted label. Smaller than `ui:StatusPill`, which carries a fill and a
mandatory icon and refuses to say its meaning in colour alone.

**That refusal is the rule to check this against.** A pill is for a status somebody acts on.
This is for an ambient fact, such as whether a folder is being watched, and the label carries
the meaning either way. If the colour is doing the talking it is a pill and belongs in one.
`Ellipse.dot` is already the mark.

## Not on the list

**The 3D model preview.** An orbiting mesh with a wireframe mode and a rig overlay needs a
renderer, not a `ControlTheme`. Nothing else on either page needs anything Avalonia cannot
draw.

**The version row.** A thumbnail, a title, an inline badge, a subtitle, a right aligned
delta and a per row action is a `ListBoxItem` with a template. It is content.

**The tokenising search field.** Parsing `tag:` and `owner:` and a leading `-` is view model
work. The field is `ui:SearchBox` with `.query`, which is built. The one control question
left open is whether the field should draw a recognised prefix differently from the rest of
what was typed, which nothing has needed yet.
