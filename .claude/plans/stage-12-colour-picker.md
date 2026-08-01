# Stage 12: colour picker

The one control in this library with no Avalonia type behind it and no way to avoid
building it. A data tool that authors materials, tints and graph colours needs it, and
the design specifies it in more detail than anything else on the theme page.

Its own stage because it is the largest single control in the plan. Folded into stage 6
it would make that stage unschedulable, and stage 6 is otherwise a day of theming.

## Depends on

Stage 4 for buttons, stage 6 for the spinbox it reuses unchanged, and stage 8 for the
shell it opens in. Last in the plan for that reason, not because it matters least.

## Goal

One picker body that works in a popover and in a panel, and a colour field that opens it.

## One body, two hosts

The rule the design states outright, and the thing to build for from the first line.

| | Popover | In a panel |
|---|---|---|
| surface | `SurfaceNest2` with a popup edge | the panel's own surface |
| shadow | `ShadowOverlay` | none |
| footer | Cancel and Apply, so the edit commits on purpose | reads the literal back, value applies live |

The body is identical either way. Build it as a control that knows nothing about its
host, and let the shell from stage 8 supply the frame and the buttons.

## The body

**Three shape modes.** A rectangle, a wheel, and sliders only. The rectangle is the
saturation and value field with a hue slider beside it, the wheel is the same in polar
form, and sliders only drops the field entirely for people who work in numbers.

**Four value modes**, each with its own channel ramps: RGB at 0 to 255, HSV, RAW as
floats, and OKHSL. Every mode carries the EV row, so HDR values above 1 round trip into
a colour rather than clamping on the way through. That last part is a correctness
requirement, not a display one, and it is the easiest thing here to get quietly wrong.

**Channel rows** are a monospace letter, a gradient ramp 9px tall on `LineControlDeep`
with a handle, and a spinbox. The spinbox is stage 6's, unchanged. If it needs changing,
change it in stage 6 so a channel and a property panel agree.

**Hex with alpha**, eight digits, beside a copy action.

**A screen eyedropper.** Deferred, deliberately. It needs platform specific code, that is
understood and accepted, and how it is written is a question for when this stage is
reached rather than now. Do not treat it as an open decision blocking anything earlier.

What is already known, so the work starts from it: Avalonia has no screen capture API, so
it needs an interface with one implementation per OS chosen in `WorkbenchCoreServices`,
the way every other platform difference in this project is handled. On Linux it goes
through the desktop portal rather than X11 directly, because Wayland refuses raw screen
reads.

Build the picker without it first. A picker with no eyedropper is usable, a picker that
crashes on Wayland is not.

**An old versus new chip**, one swatch split in two, so a change is visible against what
it replaces.

**Swatches and recents.** A grid with an add action, and a row of recents.

## Swatches and recents are content, not theme

The design fills both grids with the Slate palette, because a designer had to put
something there. Measured: `Destructive` appears in this section five times and every one
of them is a swatch. It is not used as chrome anywhere in the value editors.

So the grids start empty and fill from what a person saves and picks. Do not seed them
with the theme's own colours. This is the no mock data rule landing on a control whose
content happens to be colours, which is exactly the case where it is easiest to miss.

Where the saved swatches live is an application storage question, not a control one. The
control takes a list and reports an add.

## Not in this stage

Gradient editing, colour ramps over time, and palettes as documents. The design shows a
colour picker. Anything that edits a series of colours is a different control and a
different problem.

## Done when

- The same body renders in a popover and in a panel, and the only difference is the
  frame, the shadow and the footer.
- Every value mode round trips: a colour set in one mode reads back the same in the other
  three, and a RAW value above 1 survives the trip.
- The channel spinbox is stage 6's control with no changes of its own.
- Swatches and recents start empty.
- Nothing in the control writes a brush that is not a token, other than the colour being
  edited and the ramps derived from it.
