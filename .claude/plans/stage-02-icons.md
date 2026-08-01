# Stage 2: icons

Put the 48 named Box Icons into `Workbench.Ui` as geometry, and give the app one way
to draw an icon at a size and a role colour.

## Goal

`<ui:Icon Glyph="GitBranch" Size="16" />` and nothing else. No inline path data in a
view, no per call sizing arithmetic, no icon that is a different weight from its
neighbour.

## Build

**A generator, not a copy paste.** The set lives outside the repo at
`/home/jason/Seafile/gamedev-assets/icons/box-icons-pro-solid-rounded`. Write a small
tool that reads a list of wanted names, pulls `bx-<name>.svg`, and emits a
`StreamGeometry` resource dictionary plus the enum, so the two cannot drift apart.

Each file is a 24 by 24 viewBox with no fill attribute, so the shapes take their colour
from the control. When a new icon is needed, add the name to the list and rerun.

**The set is not as uniform as it looks.** Checked across the 48: ten hold more than one
element and five use `<rect>` rather than a path. So the shapes are merged in document
order and rects are rewritten as path data. Three things have to be right about that
merge, and each one fails silently rather than loudly.

- **Fill rule.** SVG fills nonzero, Avalonia's path markup fills even odd. Merged data
  must be prefixed `F1` or any glyph whose shapes overlap holes itself out.
- **Where a later path starts.** A path standing alone begins at the origin, so a
  leading relative `m` is measured from there. Concatenated behind another path it is
  measured from wherever that one ended. Four glyphs landed right outside the 24 box
  before this was handled.
- **How that is fixed.** Reset the current point by prepending `M0,0`, do not rewrite
  `m` to `M`. They look equivalent and are not: a moveto may be followed by bare
  coordinate pairs, which are implicit linetos taking their case from it, so the rewrite
  quietly turns those absolute. That broke `Cog`, a single path glyph that was never
  part of the original problem.

Be strict about what the generator accepts. An element it does not know, a fill
attribute, a viewBox that is not 24 by 24, all stop it. A silently wrong glyph is worse
than a missing one, and every one of the faults above produced a glyph that still
rendered.

```
tools/icons/                 the generator, not shipped
src/Workbench.Ui/Themes/Icons.axaml   generated, committed
```

Commit the output. The build must not depend on a path in the user's asset library.

**The control.** Avalonia ships `PathIcon`, a `TemplatedControl` with a `Data` geometry,
so the icon control should be that type with a glyph lookup added rather than a fresh
one. What is ours is the `Glyph` enum and its resolution to a geometry, not the drawing.

As built this was missed: `Icon` derives from `TemplatedControl` and declares its own
read only `Data`. It behaves correctly, so this is tidiness rather than a fault, but it
is the one control in the library that has a built in type behind it and does not use
it. Deriving from `PathIcon` and dropping the local `Data` is the change. Do it when
next in this file.

An `Icon` control in `Workbench.Ui` with a `Glyph` property and a
`Size`. It wraps the path in a fixed 24 by 24 `Canvas` inside a `Viewbox`, which is
the shape the existing `PathIcon` control theme already uses and the reason the
launcher chevron is the right size. `.claude/avalonia.md` records why the stock
template is wrong here: its `Stretch="Uniform"` scales by ink and discards the viewBox,
so two icons at the same nominal size come out different.

Prefer an enum over a string for `Glyph`. A typo should not compile.

**Sizes.** 16 for tables, trees, the status bar and inline chips. 20 for the activity
rail, tool cards and empty states. Both become named resources in stage 1's scalar set,
so a view never writes the number.

**Colours.** Default `InkSecondary`, `InkPrimary` on a hovered or active row, `Accent`
when selected, and the semantic marks for state icons. The icon takes its colour from
the control it sits in rather than setting its own, so a hover on the row moves the
icon with it.

## The first 48

The list in `theme-inventory.md`. Every one exists in the set with no substitution.
Generate exactly those and no more, so the resource dictionary stays small and the set
in use is visible.

## Not in this stage

The custom marks. The per tool launcher marks and the data type glyphs for recipe,
machine, product, attribute, stat, effect, tag and the four graph kinds are drawn in
house and none of them exist yet. The tool card in stage 5 uses a lettermark, which is
what the design shows, until a real mark is drawn.

## Done when

- The generator reproduces both outputs byte for byte. `generate.py --check` does that
  without writing, so it can be run against a checkout to prove nothing drifted.
- All 48 render at 16 and 20 with the same visual weight.
- No view file contains path data.
- Icon colour follows the parent's state without the view wiring it.

Verify by building every glyph at both sizes in a tree and measuring, not by eye. Three
checks catch what matters: no glyph resolves to a null geometry, every one measures
exactly its `Size`, and no geometry's bounds fall outside the 24 box. That last one is
what caught both merge faults, and neither was visible from the build.

Check that no two glyphs carry identical data as well. Equal data means the generator
mapped two names onto one file, which nothing else notices.

## As built

`tools/icons/` holds `icons.txt` and `generate.py`, neither shipped.
`src/Workbench.Ui/Themes/Icons.axaml` and `src/Workbench.Ui/Controls/IconGlyph.cs` are
generated and committed, so a clean checkout builds without the set present.

48 glyphs, all resolving, all measuring their size, none outside the box, no two alike.
The launcher's eight icons came off `PathIcon` and its own geometry sheet, which is
deleted. The `Icon` control theme centres the glyph in whatever space it is given, since
without that an icon in a fixed size container pins to a corner.
