#!/usr/bin/env python3
"""Turn Box Icons SVGs into the geometry dictionary and the glyph enum.

The icon set is licensed content and lives outside the repository, so the build
must never reach for it. This runs by hand, its two outputs are committed, and a
clean checkout builds without the set present.

A few glyphs are not in the set at all. A brand mark belongs to whoever owns it, so
those live in local/ inside the repository and are read from there first. They go
through the same reader and the same checks as the set, which is why local/README.md
says a mark is normalised by hand before it is committed rather than converted here.

    tools/icons/generate.py [--set <dir>] [--check]

--check reproduces both outputs in memory and reports whether the committed files
match, without writing anything.

Every file in the set is a 24 by 24 viewBox drawn with paths and rects and no fill,
so the shapes take their colour from the control. A tenth of the set uses more than
one element and a few use rects, so the shapes are merged in document order into one
geometry and rects are rewritten as path data.

The merged data is prefixed F1, which is the nonzero fill rule. SVG fills nonzero by
default and Avalonia's path markup fills even odd by default, so without it any glyph
whose shapes overlap would hole itself out. Anything that is not a path or a rect, or
that carries a fill of its own, is an error rather than a guess, because a silently
wrong glyph is worse than a missing one.
"""

import argparse
import pathlib
import re
import sys

DEFAULT_SET = "/home/jason/Seafile/gamedev-assets/icons/box-icons-pro-solid-rounded"

ROOT = pathlib.Path(__file__).resolve().parents[2]
NAMES = pathlib.Path(__file__).resolve().parent / "icons.txt"
LOCAL = pathlib.Path(__file__).resolve().parent / "local"
GEOMETRY_OUT = ROOT / "src/Workbench.Ui/Themes/Icons.axaml"
ENUM_OUT = ROOT / "src/Workbench.Ui/Controls/IconGlyph.cs"

VIEWBOX = re.compile(r'viewBox="([^"]+)"')
ELEMENT = re.compile(r"<(\w+)\b([^>]*)/?>")
ATTRIBUTE = re.compile(r'\b([\w-]+)="([^"]*)"')

# Everything the set is allowed to be made of. svg is the wrapper.
ALLOWED = {"svg", "path", "rect"}


def read_names():
    lines = NAMES.read_text().splitlines()
    names = [line.strip() for line in lines]
    names = [n for n in names if n and not n.startswith("#")]

    if len(set(names)) != len(names):
        duplicates = sorted({n for n in names if names.count(n) > 1})
        sys.exit(f"duplicate names in icons.txt: {', '.join(duplicates)}")

    return sorted(names)


def pascal(name):
    return "".join(part.capitalize() for part in name.split("-"))


def number(value):
    """Shortest exact spelling, so 3.0 writes as 3 and the output stays compact."""
    text = f"{value:.4f}".rstrip("0").rstrip(".")
    return text if text else "0"


def from_origin(data, first):
    """Make a path start where it would have started on its own.

    A path standing alone begins at the origin, so a leading relative m is measured
    from there. Concatenated behind another path it is measured from wherever that one
    ended, and the shape lands somewhere else entirely. Four of the set drifted right
    out of the 24 box before this was handled.

    The move is to reset the current point rather than to rewrite the command. Turning
    m into M looks equivalent and is not: a moveto may be followed by bare coordinate
    pairs, which are implicit linetos taking their case from it, so the rewrite quietly
    turns every one of them absolute. That broke Cog, which is a single path and was
    never part of the original problem.
    """
    if first or not data.startswith("m"):
        return data

    return "M0,0 " + data


def rect_to_path(attributes, where):
    x = float(attributes.get("x", 0))
    y = float(attributes.get("y", 0))
    w = float(attributes["width"])
    h = float(attributes["height"])

    # SVG lets one radius stand in for the other, and clamps both to half the side.
    rx = float(attributes.get("rx", attributes.get("ry", 0)))
    ry = float(attributes.get("ry", attributes.get("rx", 0)))
    rx = min(rx, w / 2)
    ry = min(ry, h / 2)

    if rx <= 0 or ry <= 0:
        return f"M{number(x)},{number(y)} H{number(x + w)} V{number(y + h)} H{number(x)} Z"

    n = number
    arc = f"A{n(rx)},{n(ry)} 0 0 1"
    return (
        f"M{n(x + rx)},{n(y)} "
        f"H{n(x + w - rx)} {arc} {n(x + w)},{n(y + ry)} "
        f"V{n(y + h - ry)} {arc} {n(x + w - rx)},{n(y + h)} "
        f"H{n(x + rx)} {arc} {n(x)},{n(y + h - ry)} "
        f"V{n(y + ry)} {arc} {n(x + rx)},{n(y)} Z"
    )


def find(directory, name):
    """A mark we supply wins over the set, so a glyph can be replaced without renaming it."""
    local = LOCAL / f"{name}.svg"
    if local.is_file():
        return local

    svg = directory / f"bx-{name}.svg"
    if not svg.is_file():
        sys.exit(f"{svg} not found. Is --set pointing at the Solid Rounded set?")

    return svg


def extract(directory, name):
    svg = find(directory, name)
    text = svg.read_text()

    box = VIEWBOX.search(text)
    if not box or box.group(1).split() != ["0", "0", "24", "24"]:
        sys.exit(f"{svg.name} is not a 24 by 24 viewBox, it is {box and box.group(1)!r}")

    shapes = []

    for element, raw in ELEMENT.findall(text):
        if element not in ALLOWED:
            sys.exit(f"{svg.name} holds a <{element}>, which this generator cannot read")

        attributes = dict(ATTRIBUTE.findall(raw))

        if "fill" in attributes:
            sys.exit(f"{svg.name} carries a fill, which would ignore the control's colour")

        if element == "path":
            if "d" not in attributes:
                sys.exit(f"{svg.name} has a path with no d attribute")
            shapes.append(from_origin(attributes["d"].strip(), not shapes))
        elif element == "rect":
            shapes.append(rect_to_path(attributes, svg.name))

    if not shapes:
        sys.exit(f"{svg.name} holds no shapes")

    # F1 is the nonzero fill rule, which is what SVG uses. Without it Avalonia fills
    # even odd and any glyph whose shapes overlap holes itself out.
    return "F1 " + " ".join(shapes)


def geometry_document(names, paths):
    out = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        "",
        "    <!-- GENERATED by tools/icons/generate.py. Do not edit.",
        "",
        "         Box Icons Pro, Solid Rounded, path data only, each on a 24 by 24 view",
        "         box. Draw one with the Icon control, which holds that box so every glyph",
        "         comes out the size it was asked for. -->",
        "",
    ]

    for name in names:
        out.append(f'    <StreamGeometry x:Key="Icon{pascal(name)}">{paths[name]}</StreamGeometry>')

    out += ["", "</ResourceDictionary>", ""]
    return "\n".join(out)


def enum_document(names):
    out = [
        "namespace Workbench.Ui.Controls;",
        "",
        "/// <summary>",
        "/// The glyphs the Icon control can draw. GENERATED by tools/icons/generate.py.",
        "/// Do not edit. Each member names a geometry in Themes/Icons.axaml, so the two",
        "/// cannot drift apart.",
        "/// </summary>",
        "public enum IconGlyph",
        "{",
    ]

    for name in names:
        out.append(f"    {pascal(name)},")

    out += ["}", ""]
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--set", default=DEFAULT_SET)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    directory = pathlib.Path(args.set)
    if not directory.is_dir():
        sys.exit(f"{directory} is not a directory")

    names = read_names()
    paths = {name: extract(directory, name) for name in names}

    documents = {
        GEOMETRY_OUT: geometry_document(names, paths),
        ENUM_OUT: enum_document(names),
    }

    if args.check:
        stale = [p for p, text in documents.items() if not p.is_file() or p.read_text() != text]
        for p in stale:
            print(f"stale: {p.relative_to(ROOT)}")
        if stale:
            sys.exit(1)
        print(f"{len(names)} icons, both outputs match")
        return

    for p, text in documents.items():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text)
        print(f"wrote {p.relative_to(ROOT)}")

    print(f"{len(names)} icons")


if __name__ == "__main__":
    main()
