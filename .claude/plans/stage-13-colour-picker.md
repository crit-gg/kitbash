# Stage 13: colour picker

The one control in this library with no Avalonia type behind it and no way to avoid
building it. A data tool that authors materials, tints and graph colours needs it, and
the design specifies it in more detail than anything else on the theme page.

Its own stage because it is the largest single control in the plan. Folded into stage 8
it would make that stage unschedulable, and stage 8 is otherwise a day of theming.

## Depends on

Stage 4 for buttons, stage 8 for the spinbox it reuses unchanged, and stage 5 for the
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
host, and let the shell from stage 5 supply the frame and the buttons.

## The body

**Three shape modes.** A rectangle, a wheel, and sliders only. The rectangle is the
saturation and value field with a hue slider beside it, the wheel is the same in polar
form, and sliders only drops the field entirely for people who work in numbers.

**Four value modes**, each with its own channel ramps: RGB at 0 to 255, HSV, RAW as
floats, and OKHSL. Every mode carries the EV row, so HDR values above 1 round trip into
a colour rather than clamping on the way through. That last part is a correctness
requirement, not a display one, and it is the easiest thing here to get quietly wrong.

**Channel rows** are a monospace letter, a gradient ramp 9px tall on `LineControlDeep`
with a handle, and a spinbox. The spinbox is stage 8's, unchanged. If it needs changing,
change it in stage 8 so a channel and a property panel agree.

**Hex with alpha**, eight digits, beside a copy action.

**A screen eyedropper.** Deferred, deliberately. It needs platform specific code, that is
understood and accepted, and how it is written is a question for when this stage is
reached rather than now. Do not treat it as an open decision blocking anything earlier.

What is already known, so the work starts from it: Avalonia has no screen capture API, so
it needs an interface with one implementation per OS chosen in `KitbashCoreServices`,
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

## What was built

`ui:ColorPicker`, `ui:ColorField` and the `ColorValue` behind both, with
`Themes/Controls/ColorPicker.axaml` over them and a COLOUR PICKER section in the gallery.
The rules are in the `kitbash-controls` skill under Colour. What follows is only what this
stage decided or departed from.

**Avalonia's `Avalonia.Controls.ColorPicker` package was read and refused.** It exists, it is
first party and it ships `ColorSpectrum` and `ColorSlider`, so the theme what Avalonia ships
rule points at it. Both carry `Avalonia.Media.Color`, which is four bytes, so a channel above
1 cannot exist in either, and `ColorView` is a tabbed spectrum and palette card with no RAW,
no OKHSL and no EV. Both shapes here are gradients and Avalonia has `ConicGradientBrush`, so
the wheel needs no bitmap. Nothing of it is taken and no package was added.

**The value is float RGBA and EV is a stored property beside it.** Two other models were
worked through and both fail: deriving EV from the peak channel reads a colour inside the
range as negative stops, and normalising the base so EV is always the peak makes a typed EV
read back as something else. So EV is stored, moving it multiplies, and a colour handed in
from outside brings its own stops with it.

**Every mode shows the value.** RGB, HSV and OKHSL clamp for display, which is all a screen
can show anyway, and only RAW reads a channel above 1 without clamping. Editing through a
clamped mode does clamp the stored value, which is the honest reading of using that mode.

**The wheel's bar carries value.** The design draws the same rainbow bar beside both shapes,
which leaves value with nowhere to go once the wheel carries hue. The wheel is also one disc
rather than the design's ring and inset disc, since the pick is one mapping, angle for hue
and radius for saturation, and drawing two metaphors over one mapping is what made the
mockup ambiguous.

**A channel ramp sweeps its own channel with the others held**, where the design draws black
to the colour. The design's own numbers were checked against this: `#E0A94A` reads RGB
224 169 74 and HSV 38 67 88, both exactly as drawn, and OKHSL 78 77 73 where the page says
41 71 72. The page's OKHSL numbers are not the reference space.

**The picker draws no eyedropper at all**, rather than one that does nothing. Stage 13
deferred it and the platform work is unchanged.

**Swatch selection is not drawn.** The design rings the swatch matching today's colour, which
needs the item to compare itself against the picker, and that is a converter and a multi
binding for an affordance nothing asked for. The swatches are still clickable.

**The picker's own frame is `ui:Popover`**, so a picker and a workspace list are the same
card and the panel case is that shell's own class. It takes the shell's header and footer
padding rather than the design's, which is 2px apart, and one popover in the app beat
matching the mockup.

Measured headless with real drawing and real pointer input: HSV and OKHSL round trip exactly
over five samples and hex to 8 bit, a wheel press due east three quarters out reads hue 0 and
saturation 0.75 with the value held, the bar reads hue at 360 reversed and value at 1 upright,
EV +1 doubles every channel and reads back +1.00, a colour of 2.5 handed in reads +1.32, both
literal rows and both footers swap with the host, Apply commits to the field and Cancel and
dismiss do not, the add tile fills a list that was empty, and the recent row stays away until
there is one. The gallery window itself was built and rendered, not just compiled.

## Done when

- The same body renders in a popover and in a panel, and the only difference is the
  frame, the shadow and the footer.
- Every value mode round trips: a colour set in one mode reads back the same in the other
  three, and a RAW value above 1 survives the trip.
- The channel spinbox is stage 8's control with no changes of its own.
- Swatches and recents start empty.
- Nothing in the control writes a brush that is not a token, other than the colour being
  edited and the ramps derived from it.
