# Slate build plan

Thirteen stages that take Kitbash from the old dark theme to the Slate visual
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
| `Theme Slate.dc.html` | the window, the control inventory, panels and the value editors |
| `Theme Slate - Surfaces.dc.html` | the tree, both grids, docking and the overlays |
| `Theme Slate - Toasts.dc.html` | toasts, their eight regions, and in content alerts |
| `Workbench Launcher.dc.html` | the launcher shell, the activity rail and the workspace page |
| `Engine Installs.dc.html` | the engines page the rail opens, planned in `engine-installs.md` |
| `Engine Installs - Spec.dc.html` | the written specification for that page |
| `Workbench Settings v3.dc.html` | the settings window every app opens, planned in `settings-schema.md` |
| `Foundry Editor.dc.html` | the tool that consumes the library, not built here |
| `icons/` | 48 named SVGs, all present in the Box Icons set |

**The design moves.** The launcher was redrawn after these plans were written, which
turned it into a shell with an activity rail and added two pages that have no stage yet.
Read the file before working a stage rather than trusting the stage's summary of it, and
when it has moved, amend the stages it affects rather than only the one being worked.

**Read the right file, and check it came back whole.** The theme page was one document
until it crossed `DesignSync.get_file`'s 256 KiB cap, which truncates without failing and
cost stage 5 most of its Overlays detail. It is three documents now, listed above, and
each reads whole with room to spare. Check the `truncated` flag and that the content ends
on `</html>`. Copies sit in `~/.cache/workbench-design` with a README, as a fallback
rather than a source of truth.

The design project's own `CLAUDE.md` says the Nocturne design system was unbound and
must not be re added. Nocturne is a separate project in the same account. Ignore it.

## The shared library

Launcher specific things stay in `src/Kitbash`. Everything else moves to a new
project so tools get the same look without depending on the launcher.

```
src/Kitbash.Ui/            styles, control themes, custom controls, icons
src/Kitbash/               launcher only, references Kitbash.Ui
src/tools/<tool>/            each tool references Kitbash.Ui
```

`Kitbash.Ui` is named for the rule in the root `CLAUDE.md` that `Kitbash` prefixes
anything above the tool level. It takes a reference to Avalonia and to
`Kitbash.Core`, never the other way round. `Kitbash.Core` stays lean, with Tomlyn
as its only dependency, so nothing in the contract pulls in a UI framework.

What counts as launcher specific: the workspace selector, the engine strip, the git
strip, the tool card list and the empty state. What counts as library: every control,
every brush, every icon, the window shell, the activity rail and the overlay surfaces.

The rail moved to the library side when the launcher was redrawn around one. A tool will
want the same shell, so it is not the launcher's.

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
- **Theme what Avalonia already ships. Build only what it does not have.** Every basic
  control is a built in Avalonia type with a `ControlTheme` over it: buttons, split
  buttons, dropdowns, text fields, checkboxes, radios, toggles, sliders, steppers,
  progress bars, labels, list boxes, trees, tabs, menus. A hand built control is for
  something Avalonia has no type for, such as the chip and the status pill in stage 4.
  Before writing a control, name the built in type it should have been.
  - Theme the whole type rather than reaching past it. If a control publishes settings
    for a theme to drive, such as `ProgressBar.TemplateSettings`, use them, and take the
    values it computes rather than forcing the design's numbers by owning the control.
  - A control theme is found by the exact type and never falls back to a base type, so a
    derived type such as `DropDownButton` needs its own even though it is a `Button`.
  - Where a control only resembles a shared one, give it a keyed theme rather than a
    class on the shared theme, so it does not inherit whatever that theme later gains.
- No paid Avalonia tier. `https://docs.avaloniaui.net/controls` is the list, and anything
  not marked Pro is ours to use. Verified against the Pro tier: `TreeDataGrid`, Charts,
  Markdown, MediaPlayer, On Screen Keyboard and RichTextEditor are paid. Everything else
  on that page ships in `Avalonia.Controls`, which this project already references.
- No third party control package for something this library should own. Dock in stage 12
  is the one exception and it is MIT.

- Keep the current caption button glyphs. Everything else about the title bar may
  change to match Slate.
- A window has two frames, not one. `window.nativeChrome` is a global user only setting
  that hands the frame to the desktop, which hides the caption buttons and disables the
  title bar double click. Any styling that assumes Kitbash draws the edge, the corner
  radius or the shadow has to be scoped to the `chromeless` class. Stage 3 has the
  detail.

**What the built in set gives each stage.** Checked by reflecting over
`Avalonia.Controls` 12.1.1 rather than read from a page.

| Stage | Theme these |
|---|---|
| 4 | `Button`, `SplitButton`, `DropDownButton`, `ProgressBar` |
| 6 | `TextBox`, `AutoCompleteBox`, `MaskedTextBox`, `CheckBox`, `RadioButton`, `ToggleSwitch`, `Slider`, `TickBar`, `NumericUpDown`, `ComboBox`, `CalendarDatePicker`, `TimePicker` |
| 7 | `GridSplitter`, `Expander`, `SplitView` |
| 8 | `ContextMenu`, `Menu`, `MenuItem`, `MenuFlyout`, `Flyout`, `Popup`, `ToolTip`, `Separator` |
| 9 | `ListBox`, `ListBoxItem`, `TabControl`, `TabItem`. **Not `TreeView`**, which cannot virtualise, so the tree is `ui:Tree` over `ListBox` |
| 11 | `ListBox` and `ListBoxItem` again, under both grids. **Not `TableView`**, which is decided against in that stage |

Six things in this plan have no built in type and stay hand built: the chip, the status
pill and the badge in stage 4, the range slider in stage 8, the toast in stage 10 and the
colour picker in stage 13. Two of those are hand built by decision rather than by absence.
Toast libraries exist and none is taken, because a toast here is a control with an injected
service behind it rather than a global. Avalonia does ship a colour picker, in a package of
its own, and stage 13 records why none of it is taken: its value is four bytes and this one
holds four floats. Stage 12's docking surface comes from Dock.

`ui:Tree` in stage 9 is ours because the built in type cannot do the one thing that
matters. It is a `ListBox` subclass, since `TreeView` does not virtualise and there is no
virtualising tree panel in the box.

**Both grids in stage 11 are ours by decision.** `TreeDataGrid` is paid and `TableView` is
flat, so the tree grid had no built in type either way, and taking `TableView` for the flat
one would leave two column models to keep in step. The reasoning and what the choice costs
are in that stage. Everything else in stages 4 to 11 is a theme over the list above.

Two near misses worth naming, because both look hand built and are not. A stepped slider
is `Slider` with `TickFrequency` and `IsSnapToTickEnabled` and a `TickBar` whose `Fill`
is the tick colour. A value editor's adorner is `NumericUpDown.InnerRightContent`, not a
custom well.

## Stages

| Stage | File | Depends on |
|---|---|---|
| 1 | `stage-01-tokens-and-type.md` | nothing |
| 2 | `stage-02-icons.md` | 1 |
| 3 | `stage-03-window-shell.md` | 1, 2 |
| 4 | `stage-04-buttons-and-pills.md` | 1, 2 |
| 5 | `stage-05-overlays.md` | 1, 2, 4 |
| 6 | `stage-06-launcher-relayout.md` | 1, 2, 3, 4, 5 |
| 7 | `stage-07-panels-and-splitters.md` | 1, 3 |
| 8 | `stage-08-input-controls.md` | 1, 2, 4, 5 |
| 9 | `stage-09-lists-and-trees.md` | 1, 2, 7 |
| 10 | `stage-10-toasts-and-alerts.md` | 1, 2, 4, 5, 7 |
| 11 | `stage-11-data-grid.md` | 1, 2, 8, 9 |
| 12 | `stage-12-docking.md` | 1, 2, 5, 7, 9 |
| 13 | `stage-13-colour-picker.md` | 1, 2, 4, 5, 8 |

**The numbers are the order.** Every stage depends only on lower numbers, so the plan
runs straight through and nothing has to be scheduled around.

## Beside the stages

`settings-schema.md` is not a Slate stage. It defines settings as descriptors so a
settings window can be built over any app's settings, and it needs stage 8 for the input
controls and stage 9 for the tree, so it lands after those.

`variable-height-list.md` is not one either. It is a virtualising panel that handles rows
of different heights, which `VirtualizingStackPanel` does badly, and it is what stage 11's
grids and any tree row that wraps will need. The engines page is where it was measured.

`engine-installs.md` is not one either. It is the second page in the activity rail, and it
needs nothing above stage 10, so it can be built now. Its research is `.claude/godot-engines.md`,
which is where the measurements live.

That took one reordering. Overlays used to be stage 8 and ran after the two stages that
need it: the launcher, which opens three menus and a tooltip, and the input controls,
whose date, time and colour editors all open a picker in the shell overlays defines. So
overlays became stage 5 and the two moved down. Panels stayed at 7.

Toasts and alerts moved as well, from 13 to 10, which pushed the data grid, docking and
the colour picker up one each. It depends on nothing above 7, so it could have gone
anywhere after that, and it is the last thing in the plan that the launcher itself will
use. The three it passed are all Foundry's.

Stage 6 is the first point where the app looks like the design.

Stages 7 to 13 build what Foundry will need and are not visible in the launcher, so they
can be reordered or paused without leaving the app half themed. Stages 1 to 6 cannot,
because each is the ground the next stands on.
