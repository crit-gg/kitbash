# Stage 4: buttons and pills

The first real control themes. Everything here is a `ControlTheme` in `Workbench.Ui`
over a built in Avalonia type. Only the chip and the status pill are ours, because
Avalonia has no type for either.

## Goal

Five button kinds, three small label kinds and a progress bar, each correct in all five
states, with no view ever setting a brush.

## Build

**Buttons.** All 26px tall, 5px radius, Archivo 12px weight 600 for the label.

| Kind | Resting | Hover | Pressed | Notes |
|---|---|---|---|---|
| primary | `Accent` fill, `AccentInk` text | `AccentHover` | `AccentPressed` with `AccentInkPressed` text | the only accent flood in the system |
| secondary | `SurfaceNest2` fill, `LineSeam` border, `InkPrimary` text | `StateHover` | `StatePressed` | |
| ghost | no fill, no border, `InkSecondary` text | `StateHover` fill, `InkPrimary` text | `StatePressed` | |
| danger | `#b0454a` fill, `#fff1f1` text | `#c25055` | `#973b40` with `#ffe8e8` text | solid on purpose, the one exception to the no flood rule |
| icon | 24 by 24 minimum hit area, icon only | `StateHover` | `StatePressed` | tooltip and an accessible name are required, not optional |

**Split button.** A label part and a 24px caret part with a 1px divider between them,
the whole thing sharing one radius and one fill. The caret part darkens on its own
without the label part moving. Used by the launcher engine strip, so it has to exist
before stage 5.

**Dropdown.** A button that carries a value and a chevron, 26px, `SurfaceNest2` on
`LineSeam`. The chevron rotates 180 degrees when open. Rotation is a transform child
property, so animate `RotateTransform.Angle`, never `RenderTransform` itself.
`.claude/avalonia.md` records that animating `RenderTransform` throws at startup, and
that a keyframe animation against a `TransformOperations` value does nothing at all and
logs nothing.

**Chip.** 21px, 8px radius, `SurfaceNest3` on `LineControlDeep`, a 5px square mark and
`InkChip` text at 11px. Optionally removable, which adds a small x on the right.

**Status pill.** 17px to 18px, 8px radius, and always three parts: a coloured mark, an
icon and a label. The spec is explicit that status reads as colour plus icon plus
label, so a pill with colour alone is a defect, not a variant. Five semantic sets, from
the semantic table.

**Progress.** A determinate bar, and an indeterminate form that is the same bar with a
fill of 30 percent width travelling across it. One control, two modes.

## States

Every control gets all five: normal, hover, pressed, focus, disabled.

- Focus is a soft 2px halo of `Accent` at 30 percent, hugging the control, outside the
  border. It never replaces a border.
- Disabled flattens the fill to `SurfaceControlOff` with a `LineControlOff` border and
  `InkDisabled` text. Never opacity. The control keeps its shape.

## Precedence, which is where this will go wrong

`.claude/avalonia.md` records the measured rule. A selector that varies at runtime,
which means any class or pseudo class, binds at `StyleTrigger` and beats a plain type
selector, which binds at `Style`, whatever the order. A `ControlTheme` is weaker than
any `Style`.

Two consequences for this stage.

- Put the resting look in the `ControlTheme` and the states in its nested styles. Do not
  mix a plain type selector for the base with a class selector for the state, or the
  state wins permanently and reads as a style that cannot be overridden.
- Where two state styles are both class based, the later one wins. Order base first,
  states after.

## Done when

- A page in a scratch harness shows every button kind in every state side by side, and
  it matches the theme page.
- No view sets a brush, a radius or a height on a button.
- Tab moves focus visibly through every control, and the halo never replaces a border.
- Disabled controls keep their shape and are not translucent.

## As built

One control theme per stock type, in `Workbench.Ui/Themes/Controls`. `Button` carries
all five kinds, since they differ only in their brushes. Secondary is what a button is
when it is told nothing, and `primary`, `ghost`, `danger` and `icon` are classes.

The order inside that file is load bearing and is commented as such. Every selector in
a control theme is activated, so the later one wins and nothing else decides. Base,
base states, each kind with its own states, then disabled last so it beats all of them,
then the two kinds with no fill get their transparency back, since flattening a fill
they never had would grow a box.

`SplitButton` and `DropDownButton` are both built in. A control theme is found by the
exact type and never falls back to a base type, so `DropDownButton` needs its own even
though it is a `Button` underneath. The two halves of a split button take a keyed part
theme with no fill and no border, which is what lets the caret darken alone while the
pair still reads as one control.

`Chip` and `StatusPill` are new controls, since neither has a stock type worth bending.
The pill mirrors its tier onto itself as a class, the same shape `WindowTitleBar` uses,
and each tier supplies a default glyph. That is what makes a pill showing colour alone
impossible to build rather than merely discouraged, and a caller naming its own glyph
still wins because a local value beats a control theme.

**The focus halo costs no layout.** It is a sibling border with a negative margin inside
a `Panel`, rather than a border around the frame. So focus never changes a control's
height and never replaces its border. It answers `:focus-visible`, so tabbing rings a
control and clicking one does not.

That was not enough on its own. `ContentControl` clips to its bounds by default in
Avalonia 12, so the halo arranged correctly at minus two and was then cut off flush with
the border, which reads as a hard ring. Nothing is logged and the measurements all still
pass, because the element is the right size and the right colour and only the drawing is
clipped. The fix is `ClipToBounds="False"` on the control and `ClipToBounds="True"` on
the frame inside the template, so content still cannot escape while the halo can. The
defaults are measured and recorded in `.claude/avalonia.md`.

**The indeterminate bar is the built in control's own.** `ProgressBar` publishes
`TemplateSettings`, which is the contract it offers a theme: it measures the track and
gives the band width and the distance to travel, and the theme animates
`TranslateTransform.X` between them. The band is 40 percent of the track, because that is
what the control computes. The design says 30, and holding it to 30 would mean owning the
control rather than theming it.

That was not the first attempt, and the first one is worth recording because it looked
right by every measurement. Stretching the band, scaling it to 0.3 and animating
`RenderTransformOrigin` from one edge to the other is exact, needs no measurement, and
renders correctly to a bitmap. On screen it does not move at all. The property animates,
the offscreen render is right, and the pixels never change. A transform is applied
composition side and an origin change on its own does not reach the compositor. Nothing
is logged, and a test that reads the property passes while the user sees a static band.

**Focus on a split button is per half.** Measured: a `SplitButton` is not focusable and
its two parts are, so the halo the control carried at first could never have appeared.
Each half carries its own instead, and each rounds only its outer end so the pair still
reads as one control. That is also why the frame does not clip. A clip would have taken
both halos with it, so each half rounds itself rather than being clipped to the frame.

The halo's radius is set by a style rather than written in the template, because a value
written in a template is a local value and a local value beats every style, so the two
ends could not otherwise override it.

Both carets turn with a `TransformOperationsTransition` on `RenderTransform` rather than
a keyframe animation on `RotateTransform.Angle`. Both are correct. The transition is the
lighter of the two and is the pairing `.claude/avalonia.md` measured as working.

Tokens this stage added or changed. `FocusHaloThickness` became a `Thickness`, because a
`DynamicResource` does not convert and a `BorderThickness` given a double throws at
layout. `FocusHaloReach` and `RadiusControlHalo` are the halo's negative margin and its
radius, which is the control's plus its width so the two curves stay concentric.
`IconSizeCompact` is 12, since a pill is 17px and 16 does not fit in one. `SizeMark` is
the 5px square a chip and a pill carry. `HeightProgress` is 6 and is chosen, since the
design records no bar height.

The status pill's five tiers are Ok, Modified and Error from the semantic table, plus
Accent and Neutral. The design shows five pills naming five states of a file, which is
content rather than library, so the pill takes the tier and the caller supplies the word.

Verified in a harness built from `Workbench.Ui` alone: every kind in every state side by
side, 199 checks covering each kind's three brushes at rest, hovered, pressed and
disabled, the shape and type of each, the halo appearing on tab focus and not on pointer
focus while leaving the border alone, both carets turning and coming back, every pill
tier's four brushes and its default glyph, and the bar in both modes including the sweep
moving between two readings.

Three things the harness taught, none of them faults in the theme. Avalonia's
`Transparent` is white at zero alpha. `Color.ToString` gives a name for a known colour,
so a comparison has to write the value out. Layout snaps to whole device pixels, so a
5px mark measures 5.33 at 150 percent and a size is compared within a pixel.

The launcher is unchanged. Its `Button.action` style sits in `Window.Styles`, and a
style beats a control theme, so the old look still wins there. Stage 5 deletes it.

**One regression, caught by rerunning stage 3's probe rather than by looking.** Giving
the `Button` theme a height reached the window's caption buttons, which had relied on
having none so they could stretch to the row. They went to 26px inside a 32px bar. The
fix was to give them a keyed control theme of their own, the way the split button parts
already had one, so a caption button is no longer an ordinary button wearing classes.

The general lesson, worth applying to every stage after this one: a class on a shared
control theme inherits everything that theme ever gains. Where a control only resembles
the shared one, key its own theme. And rerun the earlier stages' probes, because this
was invisible in the build and in the new stage's own harness.
