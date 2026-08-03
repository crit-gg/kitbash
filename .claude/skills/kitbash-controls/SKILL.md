---
name: kitbash-controls
description: "Kitbash UI control rules. Icons, the six button kinds, control theme precedence and lookup, the status bar, input wells and value editors, focus and disabled states, and the gallery. Read before writing or changing any ControlTheme, input, button or icon."
---

## Icons

Box Icons Pro, Solid Rounded, one weight, no mixing in outline or duotone. Draw one with
the `Icon` control and nothing else:

```xml
<ui:Icon Glyph="GitBranch" Size="16" />
```

Sizes are 16 in tables, trees, the status bar and inline chips, and 20 in the activity
rail, tool cards and empty states. The colour is inherited, so an icon follows the row or
button it sits in. Set a foreground only when the icon carries its own meaning, such as a
semantic mark.

`Glyph` is an enum, so a name that does not exist will not compile. Both the enum and the
geometry come from `tools/icons/generate.py`, which reads `tools/icons/icons.txt`, then
`tools/icons/local/`, then the licensed set outside the repo. **To add an icon, add its
name to that list and rerun the generator.** Never hand edit `Themes/Icons.axaml` or
`Controls/IconGlyph.cs`. The outputs are committed so a clean checkout builds without the
set present, and `generate.py --check` proves they have not drifted.

**A mark that is not in the set lives in `tools/icons/local/`**, which is read first, so a
brand mark is in the repo where it belongs and a glyph can be replaced without renaming it.
A file there goes through the same reader and the same checks as the set, which means it
has to arrive already normalised: a 24 by 24 box, paths and rects, no fill. The generator
is a reader and not a converter, so a glyph cannot quietly change shape because a
conversion improved. `local/README.md` says how a mark is fitted, and records what the
Godot one cost.

The set fills 18 to 20 units of its 24 box. A mark that fills more reads as bigger and
heavier than everything beside it, so fit the longer side to 19 and centre on 12,12.

The set mixes single paths, multiple paths and rects, so the generator merges shapes and
rewrites rects. The merge has three traps that all fail silently, recorded in
`.claude/plans/stage-02-icons.md`. Read that before changing it.

## Controls

**Theme what Avalonia already ships. Build only what it does not have.** Every basic
control is a built in type with a `ControlTheme` over it. Before writing a control, name
the built in type it should have been. `https://docs.avaloniaui.net/controls` is the
list, and anything not marked Pro is ours to use. Paid, and therefore out:
`TreeDataGrid`, Charts, Markdown, MediaPlayer, On Screen Keyboard and RichTextEditor.

Only two controls here are hand built, and only because Avalonia has no type for either:
`Chip` and `StatusPill`. `Icon`, `ChromelessWindow`, `WindowTitleBar`, `DialogWindow`,
`DialogFooter`, `SurfacePanel`, `Segmented`, `PathField`, `Alert` and the three toast
controls are ours for the same reason. `SurfacePanel` is the smallest of those: a `HeaderedContentControl`
plus a footer, since no built in type carries a header, a body and a footer at once.

`Tree` is the one that is a subclass rather than a new control. It derives from `ListBox`,
because a tree that virtualises is a flat list of the rows that can be seen and Avalonia has
no virtualising tree of its own. See the `kitbash-surfaces` skill.

Theme the whole type rather than reaching past it. Where a control publishes settings for
a theme to drive, such as `ProgressBar.TemplateSettings`, use them and take the values it
computes. Holding a built in control to a number it does not compute means owning it.

One `ControlTheme` per type in `Kitbash.Ui/Themes/Controls`, all merged by
`KitbashTheme.axaml`. A view names a kind and never a value.

**Fluent sits underneath, and it is what makes an unthemed control work.** Every
executable loads `<FluentTheme />` before `KitbashTheme`, so a type this library has not
themed still has a template and still draws. That is why a `ScrollViewer` scrolls and an
`ItemsControl` lists without either appearing in `Themes/Controls`.

So a theme written here is always a replacement rather than a first look, and dropping one
in takes away whatever Fluent was doing for that type. Do not add a bare theme to a type
that is working. Measured: with Fluent removed, an `ItemsControl` has a null template and
draws nothing at all, silently.

```xml
<Button Classes="primary" Content="Add workspace" />
<Button Classes="icon" ToolTip.Tip="Settings" AutomationProperties.Name="Settings">
    <ui:Icon Glyph="Cog" />
</Button>
<ui:Chip Content="machine" Mark="{DynamicResource Data}" IsRemovable="True" />
<ui:StatusPill Status="Ok" Text="synced" />
```

Buttons are 26px and come in six kinds. Secondary is what a button is when it is told
nothing. `primary` is the accent flood, `neutral` the grey one, `danger` the third,
`ghost` carries no fill and `icon` is a 24 by 24 hit area. An icon button needs a tooltip
and an accessible name, since nothing in the theme can supply the word.

**Two buttons lead, and which one depends on reach.** A view gets at most one `primary`,
for the action the whole screen is built around. Where a view repeats the same lead action
across many cards or rows, that one is `neutral`: it outranks the bordered secondary
inside its own card without nine of them fighting each other, and it leaves the accent
free to mean selection and focus. `neutral` is the only kind whose border holds one value
through every live state, since its fill moves and its edge does not.

`neutral` is carried by every button like control: `Button`, `CompactButton` and
`SplitButton`. Only the plain button's values are read from the design, which draws the
kind once. The split button's and the compact one's are derived, and each says so where it
departs. The split button's seam is the one real departure: every other kind takes its
pressed tone there, and `neutral` takes its border instead, since it is the only kind whose
edge does not track its fill.

They do not otherwise carry the same set. Only `secondary`, `primary` and `neutral` are on
all three. `Button` adds `ghost`, `danger` and `icon`, `CompactButton` adds `link`, and
`SplitButton` adds `warn` and `danger`.

**The gaps are unbuilt rather than refused**, with two exceptions worth knowing before
closing one. `CompactButton` must never gain `danger`: it is the toast and alert button,
and the design says a toast never carries a destructive action, which is also the rule
in the `kitbash-toasts` skill. `SplitButton` has no `icon`, since it is a label and a caret
and there is no glyph only form of one. Everything else missing is simply a kind nobody has
needed yet, and adding one is a small change against the theme that lacks it.

`SplitButton` and `DropDownButton` are stock Avalonia types with a theme each. `Chip` and
`StatusPill` are ours.

**A status reads as colour plus icon plus label, never colour alone.** `StatusPill` draws
all three and none can be turned off, and each tier carries a default glyph, so a pill
that says its meaning in colour alone cannot be built. The tiers are `Ok`, `Modified`,
`Error`, `Accent` and `Neutral`.

### Two rules that decide how a control theme is written

**Order inside a control theme is the only precedence there is.** A selector that varies
at runtime, which means any class or pseudo class, binds at `StyleTrigger` and beats a
plain type selector whatever the order. So the resting look goes in the setters and every
kind and state goes in a nested style, never a mix of the two. Then all of them are
activated, the later one wins, and order alone decides. Write base, base states, each
kind with its states, then disabled last.

**A control theme is found by the exact type and never falls back to a base type.** A
type derived from one that has a theme still needs its own, which is why
`DropDownButton` has one despite being a `Button`.

A corollary worth the words: a class on a shared control theme inherits everything that
theme ever gains. The caption buttons were a class on `Button` until the `Button` theme
gained a height, which shrank them inside the title bar. Where a control only resembles
the shared one, give it a keyed theme instead. `CaptionButton`, `SplitButtonPart` and
`ChipRemoveButton` are all keyed for that reason.

### The status bar

The row along the foot of a window. `Themes/Controls/StatusBar.axaml` holds all of it, so a
tool's status bar reads the same as the launcher's. Eight rules, from the design notes under
the launcher page, and they are the reason the row looks the way it does.

1. **A readout is not a control.** The branch has no border, no chevron and no hover.
   Switching branches belongs to a git tool. `TextBlock.branch`.
2. **A zero is never drawn.** "behind 0" is a line of text that says nothing happened.
   Every count is hidden below one, so anything present is worth reading and a quiet bar
   means a quiet repository.
3. **Colour plus icon plus label, never colour alone.** Each count carries its own glyph.
   The coloured dots this replaced said their meaning in hue and nothing else.
   `StackPanel.countReadout` with `ui:Icon.countMark`, `TextBlock.count`, `TextBlock.countLabel`.
4. **What blocks work escalates out of the row.** Everything else in the bar is worth
   knowing, a conflict stops you, so it leaves the row of counts and takes a tinted chip
   with an alert glyph. `Border.conflictChip`, which restyles the count classes inside it.
5. **Two groups, because they answer two questions.** Left is identity, which branch and
   what has changed in it. Right is the branch against its remote, how far ahead or behind
   and when it last looked.
6. **One action, and one word for it.** The button says Update and the label says Updated,
   whichever of the two things happened. **This is a deliberate change from the design,
   which says fetch only and never writes.** It always fetches, and it takes the new
   commits when there is nothing local that taking them could cost. Pushing and resolving
   a conflict still belong to a git client. See the `kitbash-git` skill for the guards.
7. **Counts are pluralised properly.** "1 conflict", "2 conflicts", never "1 conflict(s)".
   Humanizer does that, through `"conflict".ToQuantity(n, ShowQuantityAs.None)`, which
   inflects the noun and leaves the number out so the view can style the two apart. Only
   nouns inflect: a count of something described rather than named, such as twelve modified
   files, keeps its word.
8. **A slow action reports on itself in place.** The glyph turns for as long as the update
   runs and the label beside it says Updating. No toast, no dialog. `ui:Icon` with the
   `spin` class from `Themes/Motion.axaml`.

`StatusBarButton` is a keyed theme rather than a class on `Button`, for the reason the
caption buttons are keyed: the row is 32px including its seam and a class would inherit
every height and padding the shared theme ever gains.

Icons here are `IconSizeStatus`, which is 13. The design draws the branch glyph at 14 and
the rest at 13, and one size across the row was preferred to a 1px difference nobody can
see. The spinner is the toasts page's 1.1 seconds rather than this page's 0.9, since one
spin speed in the app beats two.

### Inputs

Everything that takes a value sits in a **well**, `SurfaceWell`, which is darker than
anything around it and stays that tone at any depth. A well is off the ramp, like every
other control.

**A well answers the pointer on its own ramp**, `SurfaceWellHover` then
`SurfaceWellPressed`, with its edge coming up to `LineWellHover`. `StateHover` is a control
fill and would lighten a well past the surface it sits on.

**Focus lightens the well.** The design's five columns show a pressed state for a text
field, which is the field being typed in, and that is the same thing as focus in a real
control. So the two are one state here: the well goes to `SurfaceWellFocus`, the border to
`LineControl`, and the halo appears.

**A value is mono and a label is Archivo.** A plain `TextBox` holds language, so it takes
the UI family. A number, a date or a path says what it is by taking `FontFamilyMono`, which
is what the numeric and picker themes do. That split is the most visible thing about this
theme and the easiest to get wrong in a form.

**A placeholder is language whatever the value is.** `PART_Placeholder` is pinned to
`FontFamilyUi` in the template rather than following the control's own family, so a mono
field still says "No file selected" in Archivo.

**The adorner rule is the whole look of a value editor.** A stepper, a calendar mark or a
colour swatch sits *inside* the well behind a hairline, never floating outside it. Nothing
is built for it: `TextBox` and `NumericUpDown` both carry `InnerLeftContent` and
`InnerRightContent`, and a spinbox is a `NumericUpDown` over a `ButtonSpinner` over a text
field with only the frame themed.

**Two of the sliders are settings rather than code.** Stepped is `TickFrequency`,
`IsSnapToTickEnabled` and `TickPlacement`, with a `TickBar` behind the track.

**A halo's radius is grown from the control's**, through `ui:HaloRadius.Grown`, rather than
named per shape. The halo sits outside the border and a corner radius describes an outer
edge, so the halo is the control's radius plus its own thickness. Naming a token per radius
worked until the search field took 8 where every other field takes 5 and wore the 5px
control's ring. A square corner stays square, which is what keeps one end of a split
control right.

**Every field carries a right click menu**, and it is `ui:TextMenu`, an attached property
one style turns on for `:is(TextBox)`. Undo, Redo, then Cut, Copy, Paste, Delete, then
Select all, in the order every desktop prints them. A field that wants none sets
`(ui:TextMenu.Shows)="False"`.

It is code rather than a setter for the reason `ui:SearchBox` is a control: a menu item has
to do something and a control theme has no code behind. A setter value is also one shared
instance across every field the theme reaches, and a menu can target only one control at a
time.

It is a `ContextMenu` and not a `MenuFlyout`, which is what opens it at the cursor at its
own width. See the `kitbash-surfaces` skill under Overlays for why the two are not
interchangeable.

**Its state is filled in on `Opening`, and that fires from the `ContextRequested` path
only.** The public `Open` skips it, so a menu opened in code reads every row on. Right
click and the context key both go the right way.

Nothing in it is written here that the framework already knows. What is on comes from
`CanCut`, `CanCopy`, `CanPaste`, `CanUndo` and `CanRedo`, read when the menu opens, so a
password field cannot copy and a read only one cannot paste without either being named. The
shortcuts come from `PlatformSettings.HotkeyConfiguration`, which is the same object
`TextBox` matches a key against, so a hint and the key that works cannot disagree on either
OS. Delete is the one exception, since the platform lists no gesture for it.

**`ui:SearchBox`** is the one input control that is ours, and only for its clear button. A
mark and a radius would be a class, but emptying a field is behaviour and a theme cannot
carry it. Its button lives in inner content, which no control theme can reach, so the theme
styles it from a plain style and the control hears the click bubble. See
`.claude/avalonia.md` under control themes for why.

**`ui:PathField`** is a well holding one path with a browse button beside it. **It holds
exactly zero or one path**, so there is no list, no add button and no chip anywhere in it,
multiple selection is never turned on, and browsing again replaces what is there. The design
is `Theme Slate - Path Field`.

```xml
<ui:PathField Target="Folder" Path="{Binding Folder}" />

<ui:PathField Path="{Binding Source}" AllowsTyping="False">
    <ui:PathField.Filters>
        <ui:PathFilter Name="Data files" Extensions=".json, .csv" />
    </ui:PathField.Filters>
</ui:PathField>
```

`Target` is `File` or `Folder` and it decides which dialog opens, what the empty field says
and what a drop will take. Everything else about the two is the same, including the glyph.

**Filters belong to the control, never to the person using it.** They gate the dialog and
they gate a typed path, and the quiet line under the field says them in words without being
written twice. A folder field ignores them, and so does a set where any one filter names no
extensions, since that one takes anything. Extensions are matched ignoring case on both
platforms, because a filter describes the shape of a name.

**A typed path is judged when the field is left, and typing over a refusal takes it back.**
So a half typed path never flashes red and a path being fixed is not red while it is half
fixed.

**Typing is on unless it is turned off.** `AllowsTyping="False"` is for a path a hand
written answer would be meaningless for. The well drops to the chrome tone and takes no
caret, and browse and clear both still work. **That is not the disabled look**: the text
keeps its tone. Disabled flattens both halves and drops the clear button rather than greying
it.

**The clear button stays while the field has focus**, where the design takes it away. A
press inside the well focuses the field, so a button that went on focus went under its own
press and could never be clicked. Nothing is lost by keeping it: the design overlaps it with
the text and ours is docked beside it, so no caret ever runs under it.

**One message slot and two tiers**, which is the settings rule in a control. `Problem` with
`ProblemTier="Error"` means the value will not do, and `Warn` means the value is fine and
the world is wrong, such as a path that has gone. The control writes its own `Problem` for a
filter failure and leaves a host's alone.

**Nothing in it touches a disk.** Whether a path is really there is the host's answer, and
in settings it is what a probe will report. The control only ever judges the shape of a
name.

`IsCompact` is the icon only form, for an inspector row or any column too narrow for the
word Browse. **A long path shows its head rather than its tail**: the design ellipsises from
the left so the file name stays readable, and a `TextBox` scrolls rather than trims, so the
whole path is the tooltip instead. The design's other display rule, a path shown relative to
the workspace, is not built either, since the control is handed a path and not a workspace.

**A drop takes the first item and nothing else.** One acceptable item dragged over the well
turns it into a drop target. Several are judged by the first, which is the one a drop takes.
The design says a drop of several says so in a toast, and it does not: a library control has
no toast service to reach.

Its theme is in `Themes/Controls/TextBox.axaml` with the other fields, since it is a well
and it shares the clear button with the search field. The clear button lives in inner
content, so it is shown from a plain style outside the theme for the reason the search
field's is.

**`ui:Segmented`** is a row of radios with a thumb behind them, and the thumb is the reason
it is a control. A border holds one child, and a fill on the chosen option would appear and
disappear where a thing that slides has to be one thing that moves. The options stay radios,
so grouping, clicking and the keyboard are all the framework's.

**No option carries a fill, in any state.** An option changes ink and nothing else, over the
design's 140ms, and the raised surface is the thumb. That covers hover and pressed as well
as the chosen one, which makes this the one control in the library that answers the pointer
without a fill. A hover fill here is a second raised surface beside the thumb, saying the
same thing in the same way, and the one that moves stops being the thing being watched.
Both the thumb's position and its width move, since Table, Grid and Cards are three
different widths.

**The thumb reads the option it is standing on** rather than working a place out from
tokens, so the padding, the 2px gap and the row height are written in the theme and nowhere
else. The motion is the theme's too, through `ThumbTransitions`, which is the shape
`ToggleSwitch.KnobTransitions` already has. It is 200ms on the knob's own curve. The two
share a curve and not a duration, because the knob crosses 13px and a thumb crosses a row.

**Only a change of answer slides.** A first placement, a layout change and coming back into
a tree are all written with the transitions off, so a row opens with the thumb already on
the chosen option instead of sliding in from the left edge, and a thumb never trails the row
while a window is being dragged. Measured over three options at 47.33, 41.33 and 50 wide: at
load the thumb is at 0 and 47.33 wide, the frame an option is picked it has not moved,
90ms later it is at 87.01 and 49.84, and it settles on 92.67 and 50. An option grown to 110
takes 110 in the frame it grew, and a row taken out of the tree and put back is exact in the
frame it returns.

**The pickers read the culture and nothing here writes a date or a time format.** Measured
across four: en-US reads 7/28/2026 and starts its week on Sunday, de-DE reads 28.07.2026 and
starts on Monday, ja-JP reads 2026/07/28. The design's Mo Tu We is one locale rather than a
specification.

The clock goes with it. Avalonia defaults `ClockIdentifier` to twelve hours whatever the
culture says, so `ui:Clock` asks the culture instead and the field reads through the
culture's own short time pattern rather than a format written here. Measured: en-US gives a
twelve hour clock, hours 1 to 12, a period column and a field reading 2:05 PM, while de-DE,
ja-JP and fr-FR give twenty four hours, hours 0 to 23, no period column and 14:05. Reading
the hour and the minute out of the control's own parts and joining them with a colon, which
is what this did first, drops the period and fixes an order that is not ours to fix.

**A picker's popover has to be told what the field holds.** The calendar opens on today
whatever the field says, and a two way binding from the calendar back to the field wipes the
date on the way in, because the calendar starts empty and pushes that back. So nothing is
bound to `PART_Calendar` and `ui:Picker` syncs it when the popover opens: the selected date,
or today when there is none. Emptying the field is the Clear button in the popover rather
than something that happens on the way in.

**A picker template has to carry every part the control names, even the ones it will never
draw.** The build refuses a template that leaves one out, and `TimePicker` throws a null
reference from `SetGrid` for a part that is present but not wrapped in the `Border` host it
expects. Its readout is the control's own hidden grid, read back through two element name
bindings, because that grid is a segmented row of numbers between rules where the design is
one string.

**Validation runs on `INotifyDataErrorInfo`.** A field draws an `Error` border when its
binding reports one. Measured on 12.1.1: Avalonia turns the data annotations plugin off, so
a `[Required]` attribute alone does nothing, but a source implementing
`INotifyDataErrorInfo` still reports and `DataValidationErrors.HasErrors` lands on the
control. CommunityToolkit's `ObservableValidator` implements it, so a view model gets this
by deriving from it. The `error` class is the same look for a view with no validating model
behind it.

**Fluent draws a focus ring of its own and it is switched off.** `Themes/Focus.axaml`
clears `FocusAdorner` on every control. Measured: a keyboard focus otherwise puts a black
2px border with a translucent white one inside it into the adorner layer, which is above
everything a template draws, so it lands on top of our halo. Fluent is loaded on purpose,
since it is what gives an unthemed type a template at all, and this takes back the one
thing it does that the design forbids.

### The gallery

```
dotnet run --project src/Kitbash.Gallery
```

Every control in the library, live, in one window. It is where a control theme is built
and where it is checked by hand, and it is a consumer of `Kitbash.Ui` and nothing else,
so it never references the launcher. Anything it needs from there belongs in the library.

It holds no palette and no control look of its own. Only page furniture, headings and
panels. So anything that looks wrong in the gallery is wrong in the library.

**Add every new control to it in the stage that builds the control.** A kind that is not
in the gallery is a kind nobody has looked at.

Each control shows a live sample and a row of held states, so all five can be read side
by side. A held sample carries `forceHover`, `forcePressed` or `forceFocus`, and
`GalleryWindow` turns those into pseudo classes on load. Those samples opt out of hit
testing, since a real pointer would otherwise clear the state that was pinned on them.

The gallery is also the window shell, so dragging, double clicking, resizing and the
inactive tier are all testable in it, and its title bar carries content, which is the
case that keeps the row alive when the desktop draws the frame.

### Focus and disabled

Focus is a 2px halo of the accent at 30 percent, outside the border and never in place
of one. It is a sibling with a negative margin inside a `Panel`, so focus costs no layout
and a control never changes height when it takes focus. It answers `:focus-visible`, so
tabbing rings a control and clicking one does not.

**A control that draws a halo has to turn its own clip off.** `ContentControl` clips to
its bounds by default in Avalonia 12, which cuts the halo off flush with the border and
leaves a hard ring. So the theme sets `ClipToBounds="False"` on the control and
`ClipToBounds="True"` on the frame inside the template, which keeps content in while
letting the halo out.

**Ring the thing that actually takes focus.** A `SplitButton` is not focusable and its
two halves are, so each half carries its own halo and rounds only its outer end. That is
why the frame does not clip: a clip would take both halos with it.

**A press on anything that is not a control takes the focus off the one that held it.**
`ChromelessWindow` is `Focusable`, which is the whole of it. Avalonia walks up from
whatever was pressed and focuses the first thing that can take focus, and it gives up when
there is none, so the window is the last stop. A press on a button still stops at the
button. A press on a panel, a heading, a scrollbar or the title bar reaches the window and
the field behind loses its caret. See `.claude/avalonia.md` under Input for what was
measured.

A window that does not derive from `ChromelessWindow` does not get this.

Disabled flattens the fill to `SurfaceControlOff` with a `LineControlOff` border and
`InkDisabled` text. Never opacity. A kind with no fill keeps none.

Each tool is its own executable, started by Kitbash as a separate OS process.
Tools are not loaded in process and Kitbash does not construct their windows.
`Kitbash.Core` is the contract shared by Kitbash and every tool. Its only
dependency is Tomlyn, for settings. Keep it that lean.

Opening a tool goes through `IToolActivation`, which says nothing about how a tool
opens. Most tools will start another application, some will run a script, and some
will open a web page. The launcher asks the tool to activate itself and reports the
`ToolActivationResult` that comes back.

`Program.cs` builds the Avalonia app. `App.axaml.cs` composes the tool registry and
opens `LauncherWindow`. Tools are registered explicitly in `App.BuildRegistry`
rather than discovered by assembly scanning, so adding one is a visible code change.

