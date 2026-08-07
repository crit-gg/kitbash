---
name: kitbash-appmark
description: "The app mark every Kitbash tool wears. The badge, the joint, the wells, the letters and the palette, what a tool may change and what it may not, and the generator that draws it. Read before making or changing a tool's icon."
---

## What a mark is

Two parts cut by a dovetailed joint, a near black well in each, one letter to a
well. The launcher keeps its own three block hexagon and is not a lettered
badge. Every tool is.

A tool changes two things and nothing else: **its two letters and its two
hues**. Everything below is the family and is the same for all of them, which
is what lets a person tell two tools apart in a dock without being able to say
why they look related.

## Drawing one

```
tools/appmark/generate.py SP --left 150 --right 30 \
    --chroma 0.148 --right-chroma 0.168 \
    --out ../splice/icons --also ../splice/splice-icon.png
```

It writes `icon.svg`, six PNG sizes and `icon.ico`. `--also` copies the 256 to
wherever the tool's manifest icon lives, which is beside `kitbash-tool.json`
and must be a plain file name with no separator in it.

`--check` draws everything in memory and compares, writing nothing. Exit 1
means the files on disk have drifted, which usually means somebody hand edited
an icon rather than moving the numbers.

**The generated files are committed in the tool's own repository**, not here.
This repository owns the drawing, each tool owns its art.

Needs `fontTools`. Rasterising probes for `rsvg-convert`, `inkscape` and
`magick` in that order and uses the first one installed. With none of them the
SVG is still written and the rest is reported as skipped, so a machine without
one is not blocked.

## The badge

| | |
|---|---|
| canvas | 1024, mark at 94% of it, so 68px of margin |
| outer corner | 170 |
| joint | 110 wide, 32 of taper, 130 deep, 18 of gap |
| well | 330 by 404, corner 24, OKLab lightness 0.20 neutral |
| letters | Archivo Bold, cap 272, OKLab 0.97 |

**Both halves take one OKLab lightness.** Two hues at one lightness weigh the
same, so neither half reads as the front one and the joint stays the thing you
look at. Setting lightness in HSL does not do this: an HSL green at 50% is far
lighter than an HSL red at 50%, and the badge tips.

**The gap is left open.** Background shows through it. That is what makes the
two parts read as two parts rather than as one tile with a line on it.

## Two rules the geometry enforces

**A cut has to clear the well by 40 and stay 40 off the top edge, and those
two demands close on each other as the well grows.** `seat` works out where a
cut can sit and returns nothing when it cannot. Past about 364 of well there
is no legal position left for a 130 wide cut, so the 404 well the family uses
takes a 110 wide one. Change the well and the joint has to give way. The
generator refuses rather than drawing an overlap.

**A letter is centred on its ink, not on its advance width.** The side
bearings of S and P are not equal, so centring on the advance puts one letter
visibly off in its well. `Glyphs.draw` measures the outline.

## What the file may contain

Flat fills and outlines. **No gradient, no stroke, no `<text>`.** The letters
are converted to paths, so the file carries no font dependency and renders the
same everywhere. The `.ico` holds PNG frames rather than bitmaps, which
Windows has read since Vista and which is a twentieth of the size.

**Two fills that only meet along an edge leave a hairline**, because each
covers about half the pixels on it and the background shows between. Nothing
in this mark relies on that: the wells and letters are painted over whole
parts, and the only place the background is visible is the gap, where it
belongs. Anything added to the badge later has to keep that property.

## Chroma is not always available

sRGB has less room at some hues than others, and green runs out before red
does. `hexof` reduces chroma until the colour fits rather than clipping the
channel, so a hue that cannot reach the number stays the lightness it was
asked for and loses a little saturation instead. The generator says so on
stdout when it happens. A pair where one hue clipped and the other did not is
worth moving, since the two halves stop matching.
