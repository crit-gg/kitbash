#!/usr/bin/env python3
"""Draw the icon files for a mark that is an SVG somebody keeps by hand.

generate.py draws a tool's badge and its icon files together. The launcher's
mark is not a badge and is not generated, so this takes an SVG that already
exists and writes the six PNG sizes and the ICO beside it.

    tools/appmark/rasterise.py icons/icon.svg --ico 16,24,32,48,64,128,256

--check draws everything in memory and reports whether what is on disk still
matches, which is what proves the PNGs have not fallen behind the SVG.

Rasterising needs one of rsvg-convert, inkscape or magick on PATH. Which one
drew a file shows in its bytes, so a set redrawn on another machine can differ
without the mark having changed.
"""

import argparse
import pathlib
import sys
import tempfile

import generate


def sizes(text):
    return [int(n) for n in text.split(",") if n.strip()]


def main():
    parser = argparse.ArgumentParser(
        description="Draw the icon files for an SVG mark.")
    parser.add_argument("svg", type=pathlib.Path, help="the mark to draw from")
    parser.add_argument("--png", type=sizes, default=generate.SIZES,
                        help="the PNG sizes to write beside it")
    parser.add_argument("--ico", type=sizes, default=None,
                        help="the frames the ICO holds, defaults to the PNG sizes")
    parser.add_argument("--check", action="store_true",
                        help="compare with what is on disk and write nothing")
    args = parser.parse_args()

    if not args.svg.is_file():
        sys.exit(f"{args.svg} is not there")
    out = args.svg.parent
    frames = args.ico or args.png

    name, run = generate.rasteriser()
    if not name:
        tried = ", ".join(n for n, _ in generate.RASTERISERS)
        sys.exit(f"no rasteriser found, tried {tried}.")

    differ = []
    with tempfile.TemporaryDirectory() as tmp:
        drawn = {}
        for size in sorted(set(args.png) | set(frames)):
            png = pathlib.Path(tmp) / f"icon_{size}x{size}.png"
            generate.subprocess.run(run(args.svg, png, size), check=True,
                                    capture_output=True)
            drawn[size] = png
            if size not in args.png:
                continue
            target = out / png.name
            if args.check:
                if not target.exists() or target.read_bytes() != png.read_bytes():
                    differ.append(png.name)
            else:
                target.write_bytes(png.read_bytes())

        ico = pathlib.Path(tmp) / "icon.ico"
        generate.write_ico([drawn[s] for s in frames], ico, frames)
        target = out / "icon.ico"
        if args.check:
            if not target.exists() or target.read_bytes() != ico.read_bytes():
                differ.append("icon.ico")
        else:
            target.write_bytes(ico.read_bytes())

    if args.check:
        if differ:
            print("out of date: " + ", ".join(differ))
            return 1
        print(f"{out} matches {args.svg.name}, drawn with {name}")
        return 0

    print(f"drew {args.svg} with {name} into {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
