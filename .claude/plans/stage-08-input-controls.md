# Stage 8: input controls

The controls that take a value. None of these appear in the launcher, so this stage is
invisible in the app and is verified in a harness.

## Goal

A tool author can build a form and a filter bar without styling anything.

## Build

**Every control here is a built in Avalonia type with a control theme over it.** None of
them is written from scratch.

| Thing | Type |
|---|---|
| text field | `TextBox` |
| search field | `TextBox` with a leading icon, or `AutoCompleteBox` if it suggests |
| checkbox | `CheckBox`, whose `IsThreeState` gives the mixed state |
| radio | `RadioButton` |
| toggle | `ToggleSwitch` |
| segmented | `RadioButton` in a group, themed as a row |
| stepper, and the spinbox | `NumericUpDown` |
| slider, continuous and stepped | `Slider` |
| dropdown that picks a value | `ComboBox` |
| date field | `CalendarDatePicker` |
| time field | `TimePicker` |
| hyperlink | `Button`, in a link kind |

`MaskedTextBox` is there too if a field ever needs a pattern.

**Pick the right date family.** Avalonia ships two. `DatePicker` and `TimePicker` use
`DateTimePickerPanel`, which is the spinning column shape. `CalendarDatePicker` opens a
month grid. The design's date popover is a month grid and its time popover is three
snapping columns, so it wants `CalendarDatePicker` for date and `TimePicker` for time.
One family for both means fighting one of them.

**Dates and times read in the user's locale**, which means the day names, the first day
of the week and the field order all come from the culture rather than from the design.
The design draws `Mo Tu We` and an ISO readout, which is one locale rather than a
specification. Both controls are expected to do this already. Verify it rather than
assume it, and record what was measured.

All 26px tall unless noted, 5px radius, and all on `SurfaceWell` `#161719` rather than
a nested surface, because an input well is its own tone in this system and does not ride
the depth ramp.

**Text field.** `SurfaceWell` on `LineControl`, `InkPrimary` text at 11.5px, placeholder
at `InkMuted`. In Avalonia 12 the property is `PlaceholderText`, since `Watermark` was
renamed. Focus draws the halo and keeps the border. Disabled flattens to
`SurfaceControlOff`.

**Search field.** A text field with a leading search icon at 16px and a clear affordance
that appears once there is text.

**Checkbox.** 14px, 3px radius, well border `LineWell` `#565d66` over `SurfaceWell`.
Three states: off, on and mixed. On fills with `Accent` and draws the check in
`AccentInk`. Mixed draws a bar rather than a check. The mixed state is real here and has
to be reachable, not just styled.

**Radio.** 14px, round, otherwise the checkbox rules.

**Toggle.** A track and an 11px round knob in `InkChip`. The knob travels in 140ms. The
design uses a cubic bezier of `.32,.72,0,1`. Animate the knob's offset through a
`TranslateTransform.X`, never through `RenderTransform`, for the reason recorded in
`.claude/avalonia.md`.

**Segmented.** A row of options sharing one 5px radius and one border, the selected
option filled with `AccentTint` and `AccentTintInk`. Only the outer corners round.

**Stepper.** A numeric field with an increment and a decrement affordance. Values are
monospace, because the spec puts numbers in JetBrains Mono.

**Slider.** A 3px track, a filled portion in `Accent` and an 11px round knob. Focus
haloes the knob.

## Value editors

The theme page's own section, and the reason this stage matters to a data tool. Every
value editor is a recessed `SurfaceWell` at 26px on a 5px radius, and every value reads
in monospace so columns of numbers, dates and colours line up.

**The adorner rule, which is the whole look.** Steppers, the calendar and clock buttons
and the colour swatch sit *inside* the well behind a hairline. Nothing floats outside it.
`NumericUpDown` already supports this: it has `InnerLeftContent`, `InnerRightContent` and
`ButtonSpinnerLocation`, so the shape is theming rather than a new control.

| Editor | What it is |
|---|---|
| spinbox | `NumericUpDown`, 18px stepper column inside the well |
| colour | a field whose swatch adorner opens the picker, stage 12 |
| date | `CalendarDatePicker` with a calendar adorner |
| time | `TimePicker`, 24 hour, clock adorner |
| date and time | one well, both adorners |
| hyperlink | accent text, underlined on hover |
| slider | `Slider` |
| stepped slider | `Slider` with `TickFrequency` and `IsSnapToTickEnabled` |
| range slider | ours, see below |
| slider with spinbox | a `Slider` and a `NumericUpDown` composed |

**Two of the four sliders are configuration, not code.** Stepped is `TickFrequency`,
`IsSnapToTickEnabled` and `TickPlacement`, with a `TickBar` whose `Fill` takes
`LineControl` for the ticks behind the track. `Ticks` takes an explicit list if the stops
are irregular. Nothing is written.

**A range slider is ours.** `Slider` derives from `RangeBase`, which has one `Value`, and
its template has one `PART_Track`. Two knobs is not a setting and Avalonia ships no range
type. Build it on the same track and knob the slider theme already draws, so the two
cannot drift, and give the dragged knob the focus halo.

**A slider with a spinbox is composition**, worth a small control only because a property
panel wants the pairing dozens of times.

**Scrubbing.** The design says a spinbox value can be dragged to scrub. That is an input
behaviour on top of `NumericUpDown` rather than a look, and it is the one piece of these
editors that is neither theming nor configuration.

**One new colour.** The well takes `#1a1b1e` while focused, a touch lighter than
`SurfaceWell`. Everything else in the section is a token that already exists.

## Shared behaviour

- Focus is the 2px `Accent` halo at 30 percent, outside the border, never replacing it.
- Disabled flattens the fill and keeps the shape.
- Every control that holds a value shows that value in JetBrains Mono and its label in
  Archivo. That split is the single most visible thing about this theme and it is easy
  to get wrong in a form.

## Validation

Avalonia 12 turned off the data annotations binding plugin by default, so validation
attributes do nothing unless wired another way. Decide the error presentation now, a
`#ef6a6e` border plus a message line, and note that the mechanism behind it is an open
question rather than assuming attributes work.

## Done when

- A harness page shows every control in every state against the theme page.
- The checkbox reaches mixed, the toggle animates in 140ms, and neither throws.
- Tab order through a form is sensible and the halo is visible on every stop.
- No control sets a brush outside its `ControlTheme`.
- Every adorner sits inside its well, and none of them changes the well's height.
- A stepped slider snaps, and its ticks sit behind the track rather than over it.
- A date shown under a second locale changes its day names and its field order, measured
  rather than assumed.
