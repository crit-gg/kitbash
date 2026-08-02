# Icons we supply

Glyphs that are not Box Icons. A brand mark belongs to whoever owns it and there is no
version of it in a licensed set, so it lives here instead, in the repository, and
`generate.py` reads this folder before it reads the set.

A file here has to be what the generator already demands of the set, because it goes
through the same reader and the same checks:

- a 24 by 24 view box
- paths and rects only
- no fill anywhere, so the shape takes its colour from the control

There is no transform support and there is no view box scaling. Whatever a mark arrives
as, it is normalised once, by hand, and what is committed here is the normalised form.
That keeps the generator a reader rather than a converter, and it keeps a glyph from
quietly changing shape because a conversion improved.

## Fitting a mark to the box

The set fills 18 to 20 units of its 24 box and centres what it draws. A mark that fills
more reads as bigger and heavier than everything beside it, which is wrong even when the
mark itself is right. Fit the longer side to 19 and centre on 12,12.

## godot.svg

The Godot engine's icon, from an SVG traced off the mark. The trace arrived as a
1024 box, one path, a translate, a fill, and 626 curves in 50 KB, which is a hundred
times the detail a 16 pixel glyph can show.

Normalising it was: flatten the curves, drop the transform, scale to the 24 box, simplify
at a tolerance of 0.06 units, then fit to 19 and centre. That is 151 points and 1.7 KB.
Rendered against the original at 240 pixels the two are hard to tell apart, and at 20
pixels, which is the size the activity rail draws it, they are identical.

It is still about three times the largest glyph in the set. A traced outline carries wobble
that a drawn one does not, and simplifying harder starts to show at 240 pixels. Worth it
for one mark, not a habit to get into.

## godot_colored.svg

**Not a glyph, and it does not go through the generator.** The Godot mark in the project's
own colours, white and #478cbf and #414042. Everything above this line is about the icon
set, where a shape carries no fill and takes its colour from the control it sits in, so a
mark that owns its colours cannot go through that reader at all. `icons.txt` does not name
it and `generate.py` never sees it.

It is committed here because this is where a mark we supply belongs, beside the mono
`godot.svg` that the activity rail draws. The two are different things for different jobs:
a row of rail icons all answer the same selection state and have to tint together, and a
page header names one product and should look like it.

What consumes it is `Workbench.Ui/Themes/Marks.axaml`, a `DrawingImage` drawn with an
`Image` rather than an `ui:Icon`. **Nothing was normalised in the crossing.** Each of the
eight paths keeps its own data and its own matrix, because Avalonia reads an SVG matrix in
the order it is written and a `DrawingGroup` takes a transform. So there was no flatten, no
scale and no simplify, and the mark cannot have drifted. Rendered at 96 pixels against the
source it is the same drawing.

Regenerating is copying the paths across again. There is no lossy step to repeat.

The drawing is 1024 wide by about 966 tall, so it is not square. Draw it with
`Stretch="Uniform"` and let it centre in whatever box it is given.
