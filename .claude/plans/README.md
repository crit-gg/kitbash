# Slate build plan

Eleven stages that take Workbench from the old dark theme to the Slate visual
direction, and turn the launcher's one off markup into a control library that
every tool shares.

Read `theme-inventory.md` first. It holds the token table and the full list of what
the design covers. Every stage refers back to it rather than repeating values.

## Source of truth

The design lives in the Claude Design project **Workbench**,
`8faef49e-39a8-4237-9440-04ab9cf949f4`, read through the DesignSync tool.

| File | What it carries |
|---|---|
| `CLAUDE.md` | the written spec, palette, density and icon rules |
| `Theme Slate.dc.html` | the built control inventory, every control in every state |
| `Workbench Launcher.dc.html` | the launcher layout |
| `Foundry Editor.dc.html` | the tool that consumes the library, not built here |
| `icons/` | 48 named SVGs, all present in the Box Icons set |

The design project's own `CLAUDE.md` says the Nocturne design system was unbound and
must not be re added. Nocturne is a separate project in the same account. Ignore it.

## The shared library

Launcher specific things stay in `src/Workbench`. Everything else moves to a new
project so tools get the same look without depending on the launcher.

```
src/Workbench.Ui/            styles, control themes, custom controls, icons
src/Workbench/               launcher only, references Workbench.Ui
src/tools/<tool>/            each tool references Workbench.Ui
```

`Workbench.Ui` is named for the rule in the root `CLAUDE.md` that `Workbench` prefixes
anything above the tool level. It takes a reference to Avalonia and to
`Workbench.Core`, never the other way round. `Workbench.Core` stays lean, with Tomlyn
as its only dependency, so nothing in the contract pulls in a UI framework.

What counts as launcher specific: the workspace selector, the engine strip, the git
strip, the tool card list and the empty state. What counts as library: every control,
every brush, every icon, the window shell and the overlay surfaces.

## Rules that apply to every stage

- Build for Windows and for Linux. The design says Windows desktop, the project rule
  says both, and the project rule wins. Anything touching the filesystem, the
  environment or a window goes behind an interface as the root `CLAUDE.md` requires.
- No mock data. The design pages carry invented recipes, machines and workspaces to
  show the controls at work. Build the control, never the content.
- One value, one meaning. `theme-inventory.md` lists the places the design repeats a
  colour. Collapse those into a single token unless the two uses would ever move apart.
- Read `.claude/avalonia.md` before touching styles or window chrome. It records the
  Avalonia 12 precedence rules, the animation traps and the hit testing rule, all
  measured rather than assumed.
- Every list surface virtualises. Lists, trees, grids, dropdown popups and menus, any
  of which can be handed thousands of rows by a data tool. Only `ListBox` virtualises by
  default in Avalonia 12. `ItemsControl` and `TreeView` both fall back to a plain
  `StackPanel` and realise everything, so a virtualising panel is a deliberate choice
  every time. Stage 9 builds the flat row list the rest of them reuse.
- Controls are built in house. No paid Avalonia tier, and no third party control
  package for something this library should own. Dock in stage 11 is the one exception
  and it is MIT.
- Keep the current caption button glyphs. Everything else about the title bar may
  change to match Slate.

## Stages

| Stage | File | Depends on |
|---|---|---|
| 1 | `stage-01-tokens-and-type.md` | nothing |
| 2 | `stage-02-icons.md` | 1 |
| 3 | `stage-03-window-shell.md` | 1, 2 |
| 4 | `stage-04-buttons-and-pills.md` | 1, 2 |
| 5 | `stage-05-launcher-relayout.md` | 1, 2, 3, 4 |
| 6 | `stage-06-input-controls.md` | 1, 2, 4 |
| 7 | `stage-07-panels-and-splitters.md` | 1, 3 |
| 8 | `stage-08-overlays.md` | 1, 2, 4 |
| 9 | `stage-09-lists-and-trees.md` | 1, 2, 7 |
| 10 | `stage-10-data-grid.md` | 1, 2, 6, 9 |
| 11 | `stage-11-docking.md` | 1, 2, 7, 8, 9 |

Stage 5 is the first point where the app looks like the design. Stages 6 to 11 build
what Foundry will need and are not visible in the launcher, so they can be reordered
or paused without leaving the app half themed.
