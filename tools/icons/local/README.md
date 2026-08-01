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
