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
tool that reads a list of wanted names, pulls `bx-<name>.svg`, extracts the single
`<path d="...">` and emits a `StreamGeometry` resource dictionary.

Each file is a 24 by 24 viewBox holding one path with no fill attribute, so the data
transfers unchanged and takes its colour from the control. That is why this is
mechanical and why it should stay mechanical: when a new icon is needed, add the name
to the list and rerun.

```
tools/icons/                 the generator, not shipped
src/Workbench.Ui/Themes/Icons.axaml   generated, committed
```

Commit the output. The build must not depend on a path in the user's asset library.

**The control.** An `Icon` control in `Workbench.Ui` with a `Glyph` property and a
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

- The generator runs from a clean checkout and reproduces `Icons.axaml` byte for byte.
- All 48 render at 16 and 20 with the same visual weight.
- No view file contains path data.
- Icon colour follows the parent's state without the view wiring it.
