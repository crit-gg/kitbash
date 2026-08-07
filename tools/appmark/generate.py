#!/usr/bin/env python3
"""Draw the app mark for a Kitbash tool, and the icon files that come off it.

Every tool in the family wears the same badge: two parts cut by a dovetailed
joint, a near black well in each, one letter to a well. Only the two letters
and the two hues change from tool to tool, so a person can tell two of them
apart in a dock without being able to say why they look related.

    tools/appmark/generate.py SP --left 150 --right 30 --out ../splice/icons

It writes icon.svg, six PNG sizes and icon.ico. The SVG is flat fills and
outlines: no gradient, no stroke, no <text>, so it needs no font installed and
renders the same everywhere.

--check draws everything in memory and reports whether what is on disk still
matches, which is what proves an icon has not been hand edited since.

Needs fontTools to read the glyph outlines. Rasterising needs one of
rsvg-convert, inkscape or magick on PATH; without one the SVG is still written
and the rest is reported as skipped.
"""

import argparse
import math
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
FONT = ROOT / "src/Kitbash.Ui/Assets/Fonts/Archivo-Bold.ttf"

# The badge. These are the family and none of them are per tool.
CANVAS = 1024.0
FILL = 944.0                 # what the mark would take with no padding
SHRINK = 0.94                # and what it actually takes, leaving a margin
CORNER = 170.0               # the outer corners
KNUCKLE = 18.0               # the corners of a cut
GAP = 18.0                   # the joint, left open so the parts read as two
WELL = (330.0, 404.0)
WELL_CORNER = 24.0
CAP = 272.0                  # cap height of a letter
TENON = dict(width=110.0, taper=32.0, depth=130.0)
CLEAR = 40.0                 # the least a cut may come to a well
MARGIN = 40.0                # or to the top and bottom edges

# The palette. Both halves take one lightness, so neither reads as the front.
LIGHTNESS = 0.612
CHROMA = 0.148
WELL_INK = (0.20, 0.0, 0.0)
LETTER_INK = (0.97, 0.012, 60.0)

SIZES = [16, 32, 48, 64, 128, 256]

EDGE = (CANVAS - FILL) / 2
FAR = CANVAS - EDGE
MIDDLE = CANVAS / 2


# --- colour ----------------------------------------------------------------

def _srgb(c):
    return 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


def _oklch_rgb(L, C, h):
    a, b = C * math.cos(math.radians(h)), C * math.sin(math.radians(h))
    l_ = L + 0.3963377774 * a + 0.2158037573 * b
    m_ = L - 0.1055613458 * a - 0.0638541728 * b
    s_ = L - 0.0894841775 * a - 1.2914855480 * b
    l, m, s = l_ ** 3, m_ ** 3, s_ ** 3
    return (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)


def hexof(L, C, h):
    """OKLCH to sRGB. Chroma is reduced until it fits rather than clipped, so
    a hue with less headroom stays the lightness it was asked for."""
    while C > 0:
        if all(-0.0005 <= v <= 1.0005 for v in _oklch_rgb(L, C, h)):
            break
        C -= 0.002
    rgb = (min(1.0, max(0.0, v)) for v in _oklch_rgb(L, C, h))
    return "#%02x%02x%02x" % tuple(round(_srgb(v) * 255) for v in rgb)


def _linear(c):
    c /= 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def oklch_of(colour):
    """A hex colour as OKLCH, for taking one that was chosen elsewhere."""
    r, g, b = (_linear(int(colour[i:i + 2], 16)) for i in (1, 3, 5))
    l = (0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b) ** (1 / 3)
    m = (0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b) ** (1 / 3)
    s = (0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b) ** (1 / 3)
    L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s
    a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s
    b2 = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s
    return L, math.hypot(a, b2), math.degrees(math.atan2(b2, a)) % 360


def resolve(spec, lightness, chroma):
    """A side's colour, from either a hue on the family ramp or a hex given
    outright. A hex is taken exactly as it is, lightness included, which is
    the one way round the rule that both halves weigh the same."""
    text = spec.lstrip("#").strip()
    if len(text) == 6 and all(c in "0123456789abcdefABCDEF" for c in text):
        colour = "#" + text.lower()
        return (colour,) + oklch_of(colour) + (True,)
    try:
        hue = float(text)
    except ValueError:
        sys.exit(f"{spec} is neither a six digit hex colour nor a hue in degrees")
    return hexof(lightness, chroma, hue), lightness, chroma, hue, False


def reached(L, C, h):
    """The chroma actually available, for reporting a hue that will not fit."""
    while C > 0:
        if all(-0.0005 <= v <= 1.0005 for v in _oklch_rgb(L, C, h)):
            return C
        C -= 0.002
    return 0.0


# --- paths -----------------------------------------------------------------

def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s == "-0" else s


def _along(p, q, r):
    dx, dy = q[0] - p[0], q[1] - p[1]
    n = math.hypot(dx, dy) or 1.0
    return (p[0] + dx / n * r, p[1] + dy / n * r)


def rounded(points, radii):
    """A closed path with a quadratic fillet at each corner. A radius is
    pulled in to half the shorter neighbouring edge, so a tight corner cannot
    overrun the one next to it."""
    n = len(points)
    fitted = []
    for i in range(n):
        prev, cur, nxt = points[(i - 1) % n], points[i], points[(i + 1) % n]
        fitted.append(min(radii[i], math.dist(prev, cur) / 2,
                          math.dist(cur, nxt) / 2))
    out = []
    for i in range(n):
        prev, cur, nxt = points[(i - 1) % n], points[i], points[(i + 1) % n]
        a = _along(cur, prev, fitted[i])
        b = _along(cur, nxt, fitted[i])
        if not out:
            out.append(f"M{f(a[0])},{f(a[1])}")
        else:
            out.append(f"L{f(a[0])},{f(a[1])}")
        if fitted[i] > 0.01:
            out.append(f"Q{f(cur[0])},{f(cur[1])} {f(b[0])},{f(b[1])}")
    return "".join(out) + "Z"


def well_path(cx, cy, w, h, r):
    x0, y0, x1, y1 = cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2
    return (f"M{f(x0 + r)},{f(y0)}H{f(x1 - r)}Q{f(x1)},{f(y0)} {f(x1)},{f(y0 + r)}"
            f"V{f(y1 - r)}Q{f(x1)},{f(y1)} {f(x1 - r)},{f(y1)}"
            f"H{f(x0 + r)}Q{f(x0)},{f(y1)} {f(x0)},{f(y1 - r)}"
            f"V{f(y0 + r)}Q{f(x0)},{f(y0)} {f(x0 + r)},{f(y0)}Z")


# --- the joint -------------------------------------------------------------

def seat(well_height, width, taper):
    """How far from the middle a cut sits, or None when it cannot sit at all.

    A cut has to clear the well and stay off the top edge, and those two close
    on each other as the well grows. Past about 364 of well there is no room
    left for a 130 wide cut, which is why the tall well takes a narrow one.
    """
    outward = MARGIN + width / 2 + taper
    inward = (MIDDLE - well_height / 2) - CLEAR - width / 2 - taper
    if inward < outward:
        return None
    return MIDDLE - (outward + inward) / 2


def seam(x, reach, width, taper, depth):
    """The joint from top to bottom, as points and the radius at each."""
    points, radii = [(x, EDGE)], [0.0]
    for centre in (MIDDLE - reach, MIDDLE + reach):
        points += [(x, centre - width / 2),
                   (x + depth, centre - width / 2 - taper),
                   (x + depth, centre + width / 2 + taper),
                   (x, centre + width / 2)]
        radii += [KNUCKLE] * 4
    points.append((x, FAR))
    radii.append(0.0)
    return points, radii


# --- letters ---------------------------------------------------------------

class Glyphs:
    """Archivo Bold, as outlines. Drawn as paths rather than as text, so the
    file carries no font dependency."""

    def __init__(self, path):
        try:
            from fontTools.pens.boundsPen import BoundsPen
            from fontTools.pens.svgPathPen import SVGPathPen
            from fontTools.ttLib import TTFont
        except ImportError:
            sys.exit("fontTools is needed to read the letters: pip install fonttools")
        self._bounds_pen, self._path_pen = BoundsPen, SVGPathPen
        font = TTFont(path)
        self.font = font
        self.set = font.getGlyphSet()
        self.cmap = font.getBestCmap()
        upem = font["head"].unitsPerEm
        self.cap = getattr(font["OS/2"], "sCapHeight", None) or upem * 0.72

    def _name(self, ch):
        if ord(ch) not in self.cmap:
            sys.exit(f"Archivo has no glyph for {ch!r}")
        return self.cmap[ord(ch)]

    def draw(self, ch, cap, cx, baseline):
        """One glyph, its ink centred on cx. Centring on the ink rather than
        the advance is what puts a letter in the middle of its well: the side
        bearings of S and P are not equal."""
        name = self._name(ch)
        scale = cap / self.cap
        bounds = self._bounds_pen(self.set)
        self.set[name].draw(bounds)
        x0, _, x1, _ = bounds.bounds
        pen = self._path_pen(self.set)
        self.set[name].draw(pen)
        ox = cx - (x0 + x1) / 2 * scale
        return (f'<path transform="translate({ox:.2f},{baseline:.2f}) '
                f'scale({scale:.5f},{-scale:.5f})" d="{pen.getCommands()}"/>')


# --- the mark --------------------------------------------------------------

def mark(letters, left_fill, right_fill, glyphs=None):
    if len(letters) != 2:
        sys.exit("a mark carries exactly two letters")

    reach = seat(WELL[1], TENON["width"], TENON["taper"])
    if reach is None:
        sys.exit(f"a {WELL[1]:.0f} tall well leaves no room for a "
                 f"{TENON['width']:.0f} wide cut")

    a, b = MIDDLE - GAP / 2, MIDDLE + GAP / 2
    lp, lr = seam(a, reach, **TENON)
    rp, rr = seam(b, reach, **TENON)

    left = rounded([(EDGE, EDGE)] + lp + [(EDGE, FAR)],
                   [CORNER] + lr + [CORNER])
    right = rounded([(FAR, EDGE), (FAR, FAR)] + list(reversed(rp)),
                    [CORNER, CORNER] + list(reversed(rr)))

    half = (FILL - GAP) / 2
    seats = (EDGE + half / 2, FAR - half / 2)
    wells = "".join(well_path(x, MIDDLE, *WELL, WELL_CORNER) for x in seats)
    baseline = MIDDLE + CAP / 2
    glyphs = glyphs or Glyphs(FONT)
    ink = "".join(glyphs.draw(c, CAP, x, baseline)
                  for c, x in zip(letters, seats))

    art = (f'<path fill="{left_fill}" d="{left}"/>'
           f'<path fill="{right_fill}" d="{right}"/>'
           f'<path fill="{hexof(*WELL_INK)}" d="{wells}"/>'
           f'<g fill="{hexof(*LETTER_INK)}">{ink}</g>')
    if SHRINK != 1.0:
        art = (f'<g transform="translate({f(MIDDLE)},{f(MIDDLE)}) '
               f'scale({SHRINK:.4f}) '
               f'translate({f(-MIDDLE)},{f(-MIDDLE)})">{art}</g>')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" '
            f'viewBox="0 0 {f(CANVAS)} {f(CANVAS)}" '
            f'width="{f(CANVAS)}" height="{f(CANVAS)}">'
            f"<title>{letters}</title>{art}</svg>\n")


# --- files -----------------------------------------------------------------

RASTERISERS = [
    ("rsvg-convert", lambda exe, src, dst, n:
        [exe, "-w", str(n), "-h", str(n), str(src), "-o", str(dst)]),
    ("inkscape", lambda exe, src, dst, n:
        [exe, "--export-type=png", f"--export-width={n}",
         f"--export-height={n}", f"--export-filename={dst}", str(src)]),
    ("magick", lambda exe, src, dst, n:
        [exe, "-background", "none", str(src), "-resize", f"{n}x{n}", str(dst)]),
]


def rasteriser():
    """The first one installed wins. Probing beats naming one, since none of
    these is standard on either OS."""
    for name, argv in RASTERISERS:
        exe = shutil.which(name)
        if exe:
            return name, (lambda src, dst, n, e=exe, a=argv: a(e, src, dst, n))
    return None, None


def png_width(blob):
    """The width out of a PNG's IHDR, so a copy can be proved to be the one
    that was meant. A manifest icon that is quietly the wrong size still draws,
    which is exactly why it is worth checking."""
    if blob[:8] != b"\x89PNG\r\n\x1a\n":
        return 0
    return struct.unpack(">I", blob[16:20])[0]


def write_ico(pngs, target):
    """An ICO of PNG frames. Windows has read these since Vista and they are a
    twentieth of the size of the bitmap form."""
    blobs = [p.read_bytes() for p in pngs]
    head = struct.pack("<HHH", 0, 1, len(blobs))
    offset = len(head) + 16 * len(blobs)
    entries, body = b"", b""
    for size, blob in zip(SIZES, blobs):
        entries += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32,
                               len(blob), offset)
        offset += len(blob)
        body += blob
    target.write_bytes(head + entries + body)


def render(svg_text, out, check=False):
    """Writes the set, or compares it. Returns what differed."""
    name, run = rasteriser()
    svg = out / "icon.svg"
    differ = []

    if check:
        if not svg.exists() or svg.read_text() != svg_text:
            differ.append("icon.svg")
    else:
        out.mkdir(parents=True, exist_ok=True)
        svg.write_text(svg_text)

    if not name:
        tried = ", ".join(n for n, _ in RASTERISERS)
        print(f"no rasteriser found, tried {tried}. Wrote the SVG only.")
        return differ

    with tempfile.TemporaryDirectory() as tmp:
        source = pathlib.Path(tmp) / "icon.svg"
        source.write_text(svg_text)
        made = []
        for size in SIZES:
            png = pathlib.Path(tmp) / f"icon_{size}x{size}.png"
            subprocess.run(run(source, png, size), check=True,
                           capture_output=True)
            made.append(png)
            target = out / png.name
            if check:
                if not target.exists() or target.read_bytes() != png.read_bytes():
                    differ.append(png.name)
            else:
                target.write_bytes(png.read_bytes())
        ico = pathlib.Path(tmp) / "icon.ico"
        write_ico(made, ico)
        if check:
            target = out / "icon.ico"
            if not target.exists() or target.read_bytes() != ico.read_bytes():
                differ.append("icon.ico")
        else:
            (out / "icon.ico").write_bytes(ico.read_bytes())
    return differ


def main():
    parser = argparse.ArgumentParser(description="Draw a Kitbash tool's app mark.")
    parser.add_argument("letters", help="the two letters, such as SP")
    parser.add_argument("--left", required=True,
                        help="left part: an OKLCH hue in degrees, or a hex colour")
    parser.add_argument("--right", required=True,
                        help="right part: an OKLCH hue in degrees, or a hex colour")
    parser.add_argument("--lightness", type=float, default=LIGHTNESS)
    parser.add_argument("--chroma", type=float, default=CHROMA)
    parser.add_argument("--right-chroma", type=float, default=None)
    parser.add_argument("--out", type=pathlib.Path, required=True,
                        help="the icons directory to write")
    parser.add_argument("--also", type=pathlib.Path, default=None,
                        help="copy the 256 here too, for the manifest icon")
    parser.add_argument("--check", action="store_true",
                        help="compare with what is on disk and write nothing")
    args = parser.parse_args()

    letters = args.letters.upper()
    right_chroma = args.right_chroma or args.chroma
    left = resolve(args.left, args.lightness, args.chroma)
    right = resolve(args.right, args.lightness, right_chroma)

    # Which branch a side took is what resolve said, not what the text looked
    # like. A hex written without its hash is still a hex.
    for label, (fill, L, C, hue, literal) in (("left", left), ("right", right)):
        if literal:
            print(f"note: {label} is {fill} outright, "
                  f"OKLab lightness {L:.3f}, chroma {C:.3f}, hue {hue:.0f}.")
            continue
        got = reached(L, C, hue)
        if got < C - 1e-9:
            print(f"note: the {label} hue only reaches chroma {got:.3f} of "
                  f"{C:.3f} at this lightness, sRGB has no more.")

    apart = abs(left[1] - right[1])
    if apart > 0.02:
        print(f"note: the two halves are {apart:.3f} apart in OKLab lightness. "
              f"The lighter one will read as the front of the badge.")

    svg = mark(letters, left[0], right[0])
    differ = render(svg, args.out, check=args.check)

    if args.check:
        if differ:
            print("out of date: " + ", ".join(differ))
            return 1
        print(f"{args.out} matches")
        return 0

    if args.also:
        source = args.out / "icon_256x256.png"
        if not source.exists():
            print("no 256 to copy, so nothing was written for the manifest.")
            return 1
        blob = source.read_bytes()
        args.also.write_bytes(blob)
        if png_width(blob) != 256:
            sys.exit(f"{args.also} is not 256 wide after copying, "
                     f"which means the set is inconsistent")
    print(f"wrote {letters} to {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
