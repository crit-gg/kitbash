# Stage 4: buttons and pills

The first real control themes. Everything here is a `ControlTheme` in `Workbench.Ui`
keyed off a stock Avalonia type where one fits.

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
