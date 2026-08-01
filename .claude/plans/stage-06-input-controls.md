# Stage 6: input controls

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
| stepper | `NumericUpDown` |
| slider | `Slider` |
| dropdown that picks a value | `ComboBox` |

`MaskedTextBox` is there too if a field ever needs a pattern.

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

**Slider.** A track, a filled portion in `Accent` and a round knob. Focus haloes the
knob.

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
