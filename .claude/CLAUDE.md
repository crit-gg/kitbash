# Workbench

Cross platform desktop app hosting designer facing tools for the Slopworks Godot
project at `/home/jason/Projects/godot/slopworks/godot`.

The point of this app existing outside Godot: the Godot editor's inspector and
save/load behavior are awkward for authoring gameplay data, and designers should
be able to work without the editor open.

## Cross platform is a requirement, not a goal

Every change is written for Windows and for Linux, and on Linux for any distribution
and any desktop. Development happens on one machine, so the other cases are never
the ones being looked at. They are still the ones that break.

Before writing anything that touches the filesystem, the environment, a path, a
process, a window or a user visible directory, answer three questions:

1. **What does the other OS do here?** If the answer differs, it goes behind an
   interface with one implementation per OS, chosen by a factory in
   `WorkbenchCoreServices`. That file is the only place allowed to test the running
   OS. Never branch on the OS at a call site, and never assume a separator, a
   directory layout, a case sensitive filesystem or a shell.
2. **What does the other distribution do here?** Assume no particular distribution,
   package manager, init system, desktop or file manager. Read the standard, honor
   the environment variable, and probe for what is installed rather than naming one
   thing. `DesktopLauncherResolver` is the shape to copy: a list of candidates, the
   first one present wins, and a message that lists what was tried when none are.
3. **What happens when it is not there?** A variable can be unset, empty, relative or
   nonsense. A directory can be missing. A program can be absent. Each of those has a
   defined answer, and the answer is never a crash.

Sandboxes count as distributions. Flatpak and Snap move every user directory, and
they say so through the standard variables, so code that reads the variable works and
code that hardcodes a path does not.

State the assumption in a comment when behavior is pinned to something external, such
as a spec rule or a variable a platform always sets. Verify what can be verified here,
and say plainly what could not be tested because this machine is Linux.

## Comment and documentation style

Applies to code comments, XML docs, markdown, and anything else written as prose.

- No semicolons.
- No hyphenated words. Write "read only", not the hyphenated form. Identifiers,
  paths, and package names keep their real spelling.
- No em dashes.
- Keep language simple. Short sentences.
- No conversational context. A comment explains the code, not the discussion that
  produced it.
- Keep comments to a minimum. Prefer code that does not need one.
- Write for maintenance. Say what a reader needs in order to change the code safely.

## User facing copy

Applies to every string a person reads in the app. Labels, buttons, tooltips,
menu items, headings, placeholders, status text, error messages, dialogs.

- No em dashes.
- No semicolons. Split the sentence instead.
- Avoid hyphenated words. Reword rather than hyphenate. Proper names and product
  names keep their real spelling.
- Keep it short. A label is a few words, a message is a sentence.
- No special characters. No middle dots, arrows, ellipsis characters, bullets or
  anything else decorative. Ordinary letters, digits and plain punctuation only,
  unless asked for one directly.

## Code

No static classes, so collaborators stay replaceable. Two exceptions:

- A container for extension methods must be static. There are two, one per library:
  `WorkbenchCoreServices` and `WorkbenchUiServices`.
- Static factory methods on an instance type are fine, such as `WebAddress.Parse`
  and `ToolActivationResult.Failure`.

Dependencies arrive through the constructor. Nothing builds a collaborator inside a
method, and nothing reaches for ambient state. Filesystem access goes through
`IFileSystem` and environment variables through `IEnvironment`.

## Composition

Microsoft.Extensions.DependencyInjection. Each executable is its own composition root
and builds one provider at startup. `App.BuildServices` is the launcher's.

Core exposes registration methods rather than a container of its own:

- `AddWorkbenchIO` filesystem, environment, user directories, path display
- `AddWorkbenchPlatform` the services that differ per OS
- `AddWorkbenchApplicationStorage` settings, state and the cache for this machine
- `AddWorkbenchWorkspaces` the list of workspaces a person has added
- `AddWorkbenchWorkspace` workspace discovery
- `AddWorkbenchSettings(paths)` settings for one workspace

`Workbench.Ui` exposes one of its own, `WorkbenchUiServices`:

- `AddWorkbenchToasts` the toast service, its clock and its settings

They use `TryAdd`, so calling several is safe and a caller can substitute any service
by registering its own first.

`Workbench.Ui` takes `Microsoft.Extensions.DependencyInjection.Abstractions` for that and
nothing more, so the library asks for the contract and the application still picks the
container. `Workbench.Core` already takes the same package.

## Commits

Commits have one author, the user. Never add a coauthor trailer and never list the
agent as an author.

## Layout

```
Workbench.slnx
src/Workbench.Core/    shared contract (ITool, IToolActivation, IToolRegistry)
src/Workbench.Ui/      the look: tokens, type, control themes, fonts, the window shell
src/Workbench.Gallery/ every control, live, for building and checking the library
src/Workbench/         the launcher app, Avalonia 12
src/tools/             one project per tool, empty until the first tool is named
```

**Workbench** is the launcher and is its own executable. `Workbench` is also the
project name prefix for anything above the tool level. Tools are separately named
products and are not prefixed.

**`Workbench.Ui`** is the look, shared by the launcher and every tool. It references
Avalonia and `Workbench.Core`, and the reference only ever points that way, so the
contract stays free of a UI framework. A consumer takes one line:

```xml
<StyleInclude Source="avares://Workbench.Ui/Themes/WorkbenchTheme.axaml" />
```

That brings the tokens, the type scale, the icons and the window shell. Nothing outside
`Themes/Tokens.axaml` writes a colour, a size or a radius. Names say what a value is for
rather than what it looks like, and two names may share a value when they are genuinely
different roles, which is noted in the file where it happens.

Everything a consumer names lives in one namespace, `Workbench.Ui.Controls`, so a view
declares one xmlns:

```xml
xmlns:ui="clr-namespace:Workbench.Ui.Controls;assembly=Workbench.Ui"
```

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
`DialogFooter`, `SurfacePanel`, `Segmented`, `Alert` and the three toast controls are ours
for the same reason. `SurfacePanel` is the smallest of those: a `HeaderedContentControl`
plus a footer, since no built in type carries a header, a body and a footer at once.

`Tree` is the one that is a subclass rather than a new control. It derives from `ListBox`,
because a tree that virtualises is a flat list of the rows that can be seen and Avalonia has
no virtualising tree of its own. See Rows, lists and trees below.

Theme the whole type rather than reaching past it. Where a control publishes settings for
a theme to drive, such as `ProgressBar.TemplateSettings`, use them and take the values it
computes. Holding a built in control to a number it does not compute means owning it.

One `ControlTheme` per type in `Workbench.Ui/Themes/Controls`, all merged by
`WorkbenchTheme.axaml`. A view names a kind and never a value.

**Fluent sits underneath, and it is what makes an unthemed control work.** Every
executable loads `<FluentTheme />` before `WorkbenchTheme`, so a type this library has not
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

Buttons are 26px and come in five kinds. Secondary is what a button is when it is told
nothing. `primary` is the one accent flood, `danger` the one other, `ghost` carries no
fill and `icon` is a 24 by 24 hit area. An icon button needs a tooltip and an accessible
name, since nothing in the theme can supply the word.

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
   a conflict still belong to a git client. See Git below for the guards.
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

### Keeping the UI thread free

Nothing that touches a disk, a process or a network belongs on the UI thread. A view model
gathers what it needs on the thread pool, returns one value, and only that value touches
bound properties. `LauncherViewModel.Read` and `Apply` are the shape to copy: `Read` is the
disk half and runs anywhere, `Apply` is the screen half and runs after the await. One load
at a time behind a semaphore, since two overlapping race on what a registry holds.

The one read that stays synchronous is the first, in the constructor, because it runs before
the window exists. There is no frame to drop, and it means a window opens filled in rather
than opening empty and filling in a moment later.

The cost of this is easy to underestimate. Opening a workspace writes application state,
refreshes the registry, and resolves a name per workspace, which reads a config file and can
search four levels of a project folder. Measured warm that is 2 to 4ms, and there is no
upper bound at all on a cold cache, a busy disk or a share.

**The first popup a process opens is the expensive one.** A flyout is a window of its own.
Measured on the launcher with a 4ms heartbeat on the UI thread: the first open held it for
75.7ms and later opens for 5 to 18. About 60 of that is machinery any popup pays for and the
rest is that popup's own content. `LauncherWindow.OnOpened` pays it at launch by showing the
workspace flyout and hiding it, with the presenter held at zero opacity so nothing reaches
the screen. First open then costs 14.8ms. A window with a heavy popup should do the same.

**Starting a process is not the problem it looks like.** Running git forks the whole app,
which is over 300MB, so it looks like it should stall everything. Measured across five
spawns, the worst gap on the UI thread was 4.3ms, which is the heartbeat's own period. It
costs nothing worth avoiding.

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

**`ui:SearchBox`** is the one input control that is ours, and only for its clear button. A
mark and a radius would be a class, but emptying a field is behaviour and a theme cannot
carry it. Its button lives in inner content, which no control theme can reach, so the theme
styles it from a plain style and the control hears the click bubble. See
`.claude/avalonia.md` under control themes for why.

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

### Panels and the depth ramp

The surfaces a tool is assembled from, and the rule that keeps them readable at any depth.

**The root tone is unique.** `SurfaceRoot` belongs to the window and its chrome, which is
the title bar, the rail, a status bar, a dialog footer and a `SplitView` pane. It never
appears again inside a window, so the outermost frame is always identifiable.

**Everything below it cycles three tones and starts over.** Level 1, 2, 3, then 1 again.
Three is the shortest cycle where a grandchild never matches its grandparent, and nothing
lightens as it descends. **Each tone carries its own seam**, so a surface takes a fill and
a border together, and `Themes/Surfaces.axaml` is the only place the pairs are written.

| Level | Fill | Seam |
|---|---|---|
| root | `SurfaceRoot` | `LineSeam` |
| 1 | `SurfaceNest1` | `LineSeam` |
| 2 | `SurfaceNest2` | `LineControl` |
| 3 | `SurfaceNest3` | `LineControlDeep` |

`LineControl` and `LineControlDeep` are named for controls and are not about controls at
all. Measured across the design's control inventory: every control on `SurfaceNest2` draws
`LineSeam`. The names stay because a popup edge is the same value.

**Controls are off the ramp.** A button stays on `SurfaceNest2` and an input well on
`SurfaceWell` with a `LineSeam` border at every level, so a field reads as a field wherever
it lands and only the container moves. No control theme reads any of this.

**Nothing counts.** `ui:Surface.Level` is an inherited attached property saying which tone a
place in the tree is on, and `ui:Surface.Nests` marks an element that starts a new one. A
nesting element reads its parent's level and holds one below it, so a panel moved to another
depth is right without being told. That is not a nicety: docking reparents panels in stage
12, and a depth worked out once and kept would be wrong the moment it moved.

Set `Nests` from a control theme, which is how `SurfacePanel` and `Expander` share one
behaviour without sharing a base type, or on a plain `Border` to make a region. Set `Level`
by hand only on a container that is not itself a surface. Never walk the tree to work a
depth out.

**`ui:SurfacePanel`** is a header over a body with a footer under it. It is a
`HeaderedContentControl`, which is already the first two, and the footer is the only member
added. `DialogFooter` is not reused: it sits on the root tone and aligns right, because a
dialog's action row is chrome and a panel footer is part of the panel.

**A panel is one tone throughout.** The header and the footer sit on the body's fill and are
told apart from it by a hairline alone, which is the seam belonging to the panel's own tone.
A fill change therefore means one thing, that a different panel has been entered. The design
draws one panel whose header and footer are `SurfaceRoot`, and that one is a window frame
rather than a panel.

The header takes whatever it is given. A string is drawn as the panel title at
`FontSizeBody` weight medium, and a layout is drawn as written, which is how a title keeps
actions or a note to its right without a second slot for them.

**`SectionLabel`** is the small capitalised label over a group, with a rule filling the rest
of the row and an optional note at the end. It is a keyed theme over
`HeaderedContentControl`, so the label is the header and the note is the content. Its rule
takes the seam of the tone it stands on, through `Border.seam`, which is worth reusing for
any hairline a view draws.

**The splitter** is the hairline itself at rest, so the panes stay flush. Hover turns that
one pixel accent and dragging widens it to three, centred, so nothing shifts. A one pixel
line cannot be grabbed, so `GridSplitter` reaches seven and pulls itself back in with a
negative margin: an auto row or column then measures one while the bounds stay seven. Both
axes carry it, because `GridSplitter` resolves `Auto` privately and never writes the answer
back, so no theme can read which way it runs. Measured both ways.

**The empty panel** is a class rather than a control, `StackPanel.emptyState`, the way the
status bar readouts are. The design draws a neutral square where the glyph belongs, so the
view names the glyph.

### Rows, lists and trees

One row look, used by a list and by a tree, and the rule that keeps it readable.

**A row never carries both a fill and an outline.** Hover and selection are fills, focus is
the same halo every other control draws, and a drop target is the one dashed edge in the
theme. Selection is the tint alone, `AccentTint` with `SelectionInk` text at medium weight,
with no left marker and no border. A row wearing a tint, a border and a halo at once is what
this rule exists to prevent.

The seven states are in `Themes/Controls/List.axaml` over `ListBoxItem`, and everything else
here is built on them. Two of them are classes rather than states, because nothing in the
control says them: `modified` draws the amber square at the end of the row, and `drop` draws
the dashed outline. Drag and drop itself is stage 12.

A row is 27px in a list and 25px in a tree, both with 2px under them and the control radius
on the row itself. A list row rests at `InkSecondary` and comes up to `InkPrimary` under the
pointer, which is what the design's five columns draw.

**`ui:Tree` is a tree that virtualises, and it is ours because Avalonia has no such thing.**
`ListBox` is the only items control in Avalonia 12 that replaces its panel with a
virtualising one. `TreeView` does not, and neither does any `TreeViewItem` for its children,
so a tree of ten thousand nodes is ten thousand controls. **`TreeView` is deliberately not
themed**, so there is one tree in this library rather than two that behave differently.

```csharp
tree.ItemsSource = new TreeRows(roots, item => ((Node)item).Children);
```

`TreeRows` is the tree flattened to the rows that can be seen, and it is what a tree is
given. Expanding splices a subtree into the list and collapsing takes it out again, so the
list only ever holds rows a person could see. Collapsing forgets, and opening a row asks
what is under it again rather than trusting what it was told before.

Depth is a value on a row rather than a place in the tree of controls. `TreeItem` reads it
and turns it into three things: the frame is pushed in by `Level` times `IndentTree`, the
caret sits at the front of the frame and holds its width whether it is drawn or not, and the
guides are rendered by the row itself, since one element per level would be built and thrown
away on every scroll.

**A container is told everything in one place and untold in the same one.** `TreeItem.Follow`
is that place. A virtualising panel reuses a container for another row, so anything set when
one is prepared has to be unset there too. `ContainerIndexChangedOverride` is the case
usually missed, where a container is kept but moved. Measured over 10,200 rows: 303 recycled
rows read while scrolling, none wearing another row's state, with 7 or 8 controls realised
throughout.

**The indent is 14 and the guide falls at 8 into the band.** The design's prose says 14 and
its markup steps 21, and the two agree once the leading slot is taken out: a child there
swaps a 22 wide caret slot for a 15 wide mark, and 21 less 7 is 14. Every row here reserves
the caret slot instead, so a branch and a leaf that are siblings line up and the step is 14
outright. Measured: frames at 0, 14, 28 and labels at 30, 44, 58.

**A guide follows the tone it stands on** and stays one step quieter than that tone's seam.
The design draws a tree on the root tone, where the seam is `LineSeam` and the guide is
`LineRow`, and the ramp in the theme keeps that gap at depth. Measured, and the reason it is
not one brush: `LineRow` on `SurfaceNest2` differs by one in each channel, so a guide named
once disappears the moment a tree is put inside two panels.

**A click on the caret opens a row without changing what is picked.** The caret's hit area
handles the press and marks it handled, which stops the row selecting. A single click
elsewhere picks the row, a double click opens it, and left and right arrow do the same from
the keyboard: right opens a closed branch and steps into an open one, left closes an open
branch and goes out to the parent from a closed one.

**That hit area is the whole strip in front of the name**, not the glyph, so it is 29 wide
rather than 14 and runs from the row's edge to where the label starts. The caret pads itself
on both sides and the row pads only its right, which is why `PaddingTreeRow` looks lopsided.
Nothing moves as a result: measured before and after, labels sit at 30, 44 and 58 either
way. The indent band to the left of the row is not part of it, so clicking there picks the
row the way it does in every other tree.

**The strip answers a single left click and nothing else.** A right click and a middle click
are left to bubble, so a context menu on the row still opens over the caret. A double click
landing in the strip is swallowed rather than acted on, because two presses have already
toggled it twice and a third would leave the row where it started after flickering through
the other state on the way. Double click to open still works everywhere else on the row.

**Tabs** are `TabControl` and `TabItem` in `Themes/Controls/Tabs.axaml`, drawn from the
design's docking page with the dock's own tier left out. The open tab takes the page's
surface and the accent marker together, since either alone would be saying it in colour. A
tab is square, because it meets the page below it. Its label is mono, which is what the
design draws and is worth keeping, since a tab names a document rather than a sentence. Top
placement only.

**Whatever holds them needs two borders.** A tab strip fills its container corner to corner
and is square, so in a panel with a radius the strip and its accent marker paint straight
into the curve. `ui:SurfacePanel` is built for it: the outer border draws the stroke and an
inner one carries the radius and the clip.

`ClipToBounds` on a single stroked `Border` is not enough, which was measured rather than
reasoned about. A `Border` clips to its **outer** radius, so along a straight edge the child
sits inside the 1px stroke but at a corner it paints over it: the marker came through at
`#569eff` where the stroke should have been. Inset by the border thickness, the inner curve
clears the stroke by about 1.4px at 45 degrees, which is why two borders work and one does
not. The design says the same thing its own way, with `overflow:hidden` on the dock frame.

### Overlays

Everything that floats lives in `Themes/Controls/Overlays.axaml`: menus, context menus,
flyouts, tooltips and the popover. They all sit on `SurfaceNest2` with a `LineControl`
edge at `RadiusControl`, with `ShadowPopup` for a menu or a tooltip and `ShadowOverlay`
for a popover.

**One radius for everything that floats**, the control radius. A popover is not a larger
card, it reads as an extension of the trigger that opened it.

`ui:Popover` is a header, a body and a footer. It is both the workspace list and the
shell a picker opens in, and its `inPanel` class drops the shadow and the surface so the
same body works dropped into a property panel.

**The room a shadow needs is part of the popup's window, and it has to answer for
itself.** A popup is a real window sized to what it holds, so the transparent room the
shadow falls into is window too, and the platform hands that window every click inside
its rectangle. `ui:Popups.Room="True"` on the element carrying that room takes a
transparent fill, which is what makes it hit tested, and closes the popup on a press that
lands on it rather than on the overlay inside it. So the room behaves as the outside of
the popup, which is what it is.

Without it a dropdown cannot be closed by clicking the control that opened it. The room
reaches 18px above the visible overlay and a control is 26px tall, so most of the trigger
is under the popup's own window, light dismiss never sees the click and nothing closes.
Verified against the backend rather than assumed: Avalonia's X11 popup is an override
redirect window with no input shape and no pointer grab, so nothing makes the transparent
part of it click through.

That is why the room is `Padding` on a wrapper rather than `Margin` on the card. A margin
is outside the element and belongs to nothing that can be clicked.

**An overlay is at least as wide as what opened it.** A menu narrower than the button that
opened it reads as a mistake. `ui:Popups.MatchesTarget` puts a floor under the width, read
off the placement target the popup already holds, so an overlay with more to say is still
as wide as it needs to be. Menus and plain flyouts take it. A context menu does not, since
it belongs to whatever it was opened on and that may be a whole page.

**A wheel inside an overlay stays in it.** A popup is its own window, but its child's
logical parent is the popup, which lives in the parent window's tree, so an unhandled wheel
routes out of the popup and scrolls the page behind it. Measured: a wheel inside the time
popover scrolled the page 150px and left the popover where it was, while the same wheel
inside a dropdown did nothing, because a scroll viewer there had already taken it. Nothing
was protecting the page, one popover simply had somewhere for the wheel to land.

`ui:Popups.KeepsWheel` on an overlay's root stops it. Bubbling does the deciding, so
anything inside that wants the wheel still gets it: measured after, an hour column still
picks by wheel and the page no longer moves.

**A dialog has no scrim.** It is a real window and the window manager owns modality. A
scrim belongs only to a popup that covers a page, and it is `ui:Dimmer.Dims="True"` on
that popup's flyout. The scrim goes in the window's overlay layer, so it needs the named
layer manager above, and the popup draws over it because a popup is its own window.

**An `ItemsPanel` setter is accepted and ignored.** Measured on a `ListBox`: the property
reports the panel that was asked for while the realised panel is the default one, from a
control theme and from a local value alike, because the presenter builds its panel once
and does not rebuild. Lay items out some other way rather than assuming it took.

A `BoxShadow` cannot be cleared by a setter with an empty value, which throws at layout
rather than at build. Use `ShadowNone`.

### Toasts and alerts

How the app says something happened, and how it says something is wrong.

**If it describes something that just happened, it is a toast. If it describes the state of
what is on screen, it is an alert.** The same condition is never both. Anything a person
has to act on before continuing is an alert.

**A toast is a service taken through a constructor.** There is no static `Toast.Show`, no
ambient host and no service locator. `AddWorkbenchToasts` registers `IToastService`,
`IToastServiceFactory`, `IToastScheduler` and `ToastOptions`, all with `TryAdd`, so any of
the four can be replaced by registering it first.

```csharp
toasts.Show(new ToastRequest
{
    Tier = ToastTier.Error,
    Title = "Export failed",
    Body = "Godot 4.7.1 is not installed for this workspace.",
    Actions = [new ToastAction("Install engine", Install) { IsPrimary = true }],
});
```

**The service knows nothing about a visual tree.** It owns toasts, regions, dwell and
grouping, and `ui:ToastHost` watches it and draws. That split is the reason dwell, pausing
and grouping are checked with no window anywhere, which they are: a manual clock, 47
assertions, no Avalonia beyond the assembly reference.

**`IToastScheduler` is the whole seam.** It supplies the time, says whether the caller is
on the toast thread, runs work on it and starts the one timer. `ToastScheduler` is the only
file in the toast service that names Avalonia, and it is `Dispatcher.UIThread` and a
`Stopwatch`. Time is monotonic on purpose, so a clock correction cannot leave a toast
dwelling for an hour.

**`Show` runs on the toast thread and `Post` runs anywhere.** `Show` hands back the live
`Toast`, and it cannot, from another thread, because the answer depends on what is already
showing. A worker with something to say and nothing to follow up on calls `Post`. A worker
with long work to report raises the toast first and then writes `Progress` and `Body` from
wherever it likes, since every member of `Toast` marshals itself.

**Dwell.** 4 seconds, 8 when there is an action, and indefinite for an error, for anything
busy and for anything reporting progress. A dwell of zero or less stays until it is
dismissed. `ToastOptions` holds all of it and every other number the service behaves by.

**A toast that will not go away by itself draws no bar.** That is the rule the bar exists
for rather than a decoration. The bar says time left when a toast is counting down and work
done when it is reporting progress, which is why `Toast.Meter` has one name and not two.
**This departs from the design on the undo bar**, which the design draws without one. It is
the one short form carrying something worth pressing, and a hidden clock on a button a
person means to press is exactly what the bar prevents. The compact form has nothing to
press and keeps the design's plain pill.

**A repeat collapses rather than pushing the stack.** The same toast fired again lands on
the one already showing, takes its newer body, restarts its timer and counts. What counts
as the same is `ToastRequest.GroupKey`, and a null one is the tier, the form and the title
together, so a repeat collapses without anyone arranging it. Only onto the newest, so two
different toasts alternating stay two cards.

**Two actions, and never a destructive one.** A third is refused rather than trimmed, and
there is no destructive kind for a toast button to take. A toast is read after the fact and
often out of the corner of an eye, so nothing on one may destroy anything.

**Eight regions, each with its own stack, timers and limit.** Three deep, and the rest
collapse into a count. Top regions grow down, bottom regions grow up and the side centres
grow down from their midpoint, with the newest always nearest the anchored edge. A card
appearing top left never reflows the bottom right stack.

**A region is a deck, not a column.** The newest card is drawn whole and the ones behind
it show a strip of their top, scaled back and faded. `ui:ToastDeck` lays that out and is
ours because nothing in Avalonia does it. Three full cards would be most of a window and
would read as a list rather than as something passing through.

**Every region stacks the same way.** The newest card is at the foot of the deck and the
ones behind it pile up above it. What the anchor decides is where the deck sits and which
way it grows as cards arrive, not which end the newest is at, so a top region grows
downward with its newest card moving away from the top edge. There is no second layout to
get backwards, which is what the first attempt did: it mirrored the deck for the top and
centre regions and they came out layered the wrong way round.

A card behind is arranged as exactly the strip that shows, so the strips tile: one card's
foot is the next one's head. Cards are not all the same height, and a deck of mixed
heights cannot both put the newest on the anchored edge and show an even strip of each
card behind it. Stepping the tops leaves a short newest card floating clear of the edge,
and stepping the bottoms hides a short card behind a tall one completely. Giving each one
its strip settles both, and it is also what stops a faded card reading through to the text
of the card under it, which it did.

**A strip runs on under the card in front of it by the surface radius**, which is what
`ToastDeck.Tuck` is. A strip that stops level with the front card's edge leaves that
card's two corner curves showing the page through them, and a deck with daylight in its
corners is not a deck.

**A card behind keeps the whole shape of a card.** The tucked end is hidden rather than
changed. Squaring it off was tried and it is wrong for a reason only a corner shows: the
notch at each corner of the front card is filled by the card behind, and a square corner
with a straight edge running up through a curve reads as a card in front.

**The tuck is frame and never content.** `PART_Frame` is held back by the same amount and
clips there, so what runs on under the front card is fill alone. A card behind is faded,
so any of its own content left under the card in front reads through as a second line of
text across the deck. Measured twice, once for each way of getting it wrong.

The strip is 31, which is the card's own padding plus its status mark, so a card behind
shows a whole row rather than a mark sliced through the middle. The design draws 20 and
26 and both cut it.

The bar goes with it: a card behind draws none, since the bar lives along a foot that is
no longer there. Which card is in front is the card's own `ZIndex`, taken from its depth,
so it holds at either end of the deck.

The drawn order is the region's, not the view's: `IToastRegion.Visible` is newest first for
a region that grows down and newest last for one that grows up, so a stack is a plain
vertical list either way.

**Hovering pauses one region.** `ui:ToastStack` sets `IsPaused` on the region under the
pointer and nothing else, and the pointer reaches the stack because the cards are what it
lands on. An exit still runs while a region is paused, since a card that has been dismissed
is already gone as far as the person is concerned.

**A region belongs to whatever raised it.** The window owns the application's service and
a tool panel that reports inside itself asks `IToastServiceFactory` for another. Two
services means two sets of eight regions. `ui:ToastHost` clips, so the panel's cards stay
in the panel: measured, a 352 card in a 300 panel is cut at the panel's edge and the
window's service sees none of it. A panel narrower than a card usually wants the compact
form.

**The host is a `Panel` of eight overlaid stacks, not a three by three grid.** Measured
with the grid first and it was wrong twice: a 352 card in a third of a 1000px window was
cut off at both side edges, and three tall cards came to 280 in a 233 row, which arranged
from the top and put the newest card off the bottom of the window. A cell can be smaller
than a card and the whole surface never is. The cost is that two adjacent regions can
overlap when both are full, which is honest and rare.

**Entry and exit are separate mechanisms and must stay so.** Entry is a keyframe animation
on the card, `Opacity` and a real `TranslateTransform`. The depth scale is a
`TransformOperations` transition on `PART_Root`, a different element, because a keyframe
against a `TransformOperations` value does nothing at all and logs nothing. Exit is 120ms
and the service keeps a dismissed toast for exactly that long before removing it, so the
fade and the removal are one number.

**A card must not clip.** `TemplatedControl` clips to its bounds by default, and so does
`ItemsControl`, so the card, the stack and the stack's items control all turn it off.
Measured with the clips on: the shadow reached two pixels below a card, nothing to either
side, and left a dark wedge in each rounded corner where the card's bounds sit outside its
curve. Only the host clips, and that is what keeps a panel's toasts in the panel.

**An alert is content.** No shadow and no dwell, and it sits in the layout until the
condition goes away. The surface is the tier's tint with the tier's edge, which is why an
alert never draws a coloured bar down its left side. `IsOpen` takes it out of the layout,
so a view model binds the condition rather than wiring a click.

Three forms. **Block** is the default, wrapping, with a title, a detail and actions.
**Strip** is full bleed under a header, on the row height, one line, cut off rather than
wrapped, drawing only its own seam so whatever holds it supplies the frame. **Inline** is
attached to one field, no surface and no border, an icon and one line in the tier's own
colour.

The design's fourth form, in place, is the empty state and it was already built. It is
`StackPanel.emptyState`, on the surface it stands on rather than on a tint, so nothing was
added for it.

**Only an error recolours the control it is attached to.** It recolours the border and the
value and never the label, which is the `error` class and the `DataValidationErrors` state
on `TextBox`. A warning and a hint leave the field alone and say what they have to say in
an inline alert under it.

**An alert's two lines are strings, unlike every other header and content in this library.**
They are prose and they wrap, and a `ContentPresenter` has no way to say so. Anything
richer goes in `Actions`, which is a slot.

### The gallery

```
dotnet run --project src/Workbench.Gallery
```

Every control in the library, live, in one window. It is where a control theme is built
and where it is checked by hand, and it is a consumer of `Workbench.Ui` and nothing else,
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

Disabled flattens the fill to `SurfaceControlOff` with a `LineControlOff` border and
`InkDisabled` text. Never opacity. A kind with no fill keeps none.

Each tool is its own executable, started by Workbench as a separate OS process.
Tools are not loaded in process and Workbench does not construct their windows.
`Workbench.Core` is the contract shared by Workbench and every tool. Its only
dependency is Tomlyn, for settings. Keep it that lean.

Opening a tool goes through `IToolActivation`, which says nothing about how a tool
opens. Most tools will start another application, some will run a script, and some
will open a web page. The launcher asks the tool to activate itself and reports the
`ToolActivationResult` that comes back.

`Program.cs` builds the Avalonia app. `App.axaml.cs` composes the tool registry and
opens `LauncherWindow`. Tools are registered explicitly in `App.BuildRegistry`
rather than discovered by assembly scanning, so adding one is a visible code change.

## Settings

TOML, layered, in `Workbench.Core/Settings`. Two independent axes:

- **Scope**: `Global` for the launcher and every tool, or `ForTool(id)`. These are
  separate namespaces, not a fallback chain. A missing tool setting does not
  resolve to a global value of the same name. Tools read both.
- **Layer**: `TeamShared` then `User`, lowest precedence first.

```
<workspace root>/.workbench/
  .gitignore          generated on first user write, ignores user/
  config/             team shared, committed to git
    workbench.toml      global scope
    tools/<id>.toml     tool scope
  user/               one person's overrides, never committed
    workbench.toml
    tools/<id>.toml
```

A workspace is any directory containing `.workbench`. `WorkspacePaths.Discover`
walks up to find it the way git finds `.git`, so a tool launched from a
subdirectory resolves the same settings the launcher does.

Merging is per key, not per file. A user file holding one override does not hide
the rest of the shared config. Keys are dotted paths onto nested TOML tables, such
as `editor.font.size`. A value that exists but will not convert to the requested
type counts as absent and falls through to the layer below.

Reads go through `ISettings`. Writes go through `ISettingsService.Set`, which names
its layer explicitly because writing to `TeamShared` changes the setting for
everyone. `Apply` writes a batch to one file, so saving a page of edits is one read
and one write rather than one of each per key, and `Set` is one edit through it.
Writes rewrite the file from the model and do not keep comments, so a batch that
changed nothing writes nothing. There is no file watching, so one process does not
see another's write until it reloads.

`ISettingsDocumentStore` is the only place the file format lives. `SettingsDocument`
holds nested tables and no format specific types, so moving off TOML would touch one
class. It reads two ways: `Read` throws on a file that will not parse and `Open`
brings the failure back as a value, for a caller that draws it rather than fails.

**A file that will not parse is never written back.** This is the one hazard in the
whole system. A write reads the file, changes its keys and writes the whole document
back, and a broken file reads as an empty document, so writing it back would replace
every hand written value with almost nothing under a button that said Save changes.
`Apply` refuses on the parse result rather than on the document being empty, because
after the fact those two look identical. `SettingsFileUnreadableException` names the
file, and it is what `Read` throws as well.

### Schemas

A setting is described once, as an object, so a settings window can be built over any
app's settings without knowing anything about the app. `Workbench.Core/Settings/Schema`,
composed by `AddWorkbenchSettingsSchema`. The plan is `.claude/plans/settings-schema.md`.

**A descriptor is the one place a setting is defined**, and a reader takes its default
from it rather than passing one at the call site. `WindowSettingsSchema` is the first,
and `WindowSettings` is handed it rather than naming a key. **Hold a descriptor as an
instance member reached through a constructor.** A `public static readonly` one is the
obvious shortcut and it is ambient state.

Three levels: a `SettingsPage` is a tree node, it holds `SettingsSection`s under rule
headings, and a section holds rows. Almost every row is a `SettingDescriptor<T>`. The
other `ISettingsRow` implementation is the escape hatch an app supplies for a row that
is not a setting, such as a list of known workspaces or a button that resets the app,
so the schema never grows a way to describe a button.

**The scope belongs to the schema and the home belongs to the page.** Every page in the
launcher's window writes `workbench.toml` and every page in a tool's writes that tool's
file, so a tool cannot declare a global key by accident. `SettingsHome` is
`Application`, `Workspace` or `State`, and it decides which files stand behind a page
and whether there is a layer to choose. Only `Workspace` layers. There is no per setting
home and no per setting scope.

**A rule is data, never a predicate.** A `Func<T, bool>` can validate and nothing else,
so it cannot bound a spinner, write its own summary or be reasoned about. The set is
closed and adding to it is a deliberate change: `RangeRule<T>`, `LengthRule`,
`NotBlankRule`, `PatternRule`, `ChoiceRule<T>`, `PathShapeRule`.

**Rules, choices and probes are three different things.** `ChoiceRule` is a closed set,
so a value outside it is wrong and the layer below decides instead. `ISettingChoices<T>`
offers options discovered at runtime and never invalidates, because a pinned engine
version that is not installed is still the right value. `ISettingProbe` asks the
environment about a value that is already good, off the UI thread, and its answer
changes after the value is written.

**One message slot, two causes, and the origin says which.** A rule or conversion
failure means the value did not survive, so the origin is `Invalid` and the setting
falls back. A probe message means the value survived and the world is wrong, so the
origin stays the layer it came from. That is the whole of severity, and there is no
severity field.

**The editor is derived unless named.** A bool is a toggle, a closed choice is a segment
when it has at most three options of at most twelve characters and a dropdown otherwise,
a number is a number, an array is a list, and anything left is text. The two thresholds
are a guess rather than a measurement, so name a `SettingEditor` where they read wrong.

**`ISettingsInspector` is the read side, because `ISettings` merges and forgets.** It
opens every file behind a page and reports, per layer, what is stored and how it fared,
which layer won, whether reset would do anything, and whether each file is there and
readable. That closes three cases the merged read cannot: which layer won, that a lower
layer also holds a value, and that a stored value could not be used. Without it the
origin dot is decoration. It touches a disk, so it runs off the UI thread and caches
nothing.

**Reset removes the key, it does not write the default.** Writing the default pins
today's value and quietly opts that person out of every future change to it.
`SettingsEdit.Remove` is that, and it prunes a table it leaves empty.

**A home that is not composed is a home with nothing behind it.** `ISettingsHome` is one
per store, and `AddWorkbenchSettingsSchema` registers the two that need no workspace.
`AddWorkbenchSettings` contributes the workspace one, so a window composed without a
workspace reads its workspace pages as unavailable rather than failing on them. The
launcher changes which workspace is open at runtime, and nothing yet follows that, which
is the settings window's problem to solve when it lands.

`ISettingsWriter` saves a page. It refuses a read only page or home, a layer that does
not match whether the page layers, and any key the page does not declare, so a window
can only write what it drew.

### The schemas that exist

Three, all Core's, all in the `Application` home. That home is per user per machine and
has no layer, which is the point: **a path is right for one machine and wrong for every
other**, so none of these can be shared through a workspace's team config by accident.

| Schema | Keys |
|---|---|
| `WindowSettingsSchema` | `window.nativeChrome` |
| `ExternalToolsSettingsSchema` | `tools.git.path`, `tools.dotnet.path` |
| `GodotSettingsSchema` | `godot.engines.directory` |

**A blank override means the app decides.** `tools.git.path` and `tools.dotnet.path`
default to blank, and blank is an answer rather than a gap, so `PathShapeRule` takes
`allowEmpty`. `IExternalTools` is what resolves it: the override when it points at
something runnable, and `IExecutableFinder` on PATH otherwise, which is what the app did
before the setting existed. `GitStatusReader` and `GitUpdater` both go through it, so the
duplicated PATH lookup they each held is gone.

**An override that points at nothing falls back rather than taking the program away.** A
bad override should not be worse than no override. It is reported instead, through
`ExternalTool.ConfiguredIsMissing`, so the choice is never silently ignored. Saying it is
a probe's job when probes land.

**Where there is a knowable default, the default is the real value, not blank.**
`godot.engines.directory` defaults to `ApplicationPaths.Engines`, worked out when the
schema is built, so a window shows the folder engines actually go in and resetting means
that folder rather than meaning nothing. A program found on PATH cannot do this, since
its answer is not known until something searches, which is why those two use blank.

`ApplicationPaths.Engines` is `engines` under the data directory, which is the case
`State` was put there for. It reads oddly on Windows, where the data directory already
ends in `State`, so engines land in `%LOCALAPPDATA%\Workbench\State\engines`. Moving it
means a fourth user directory and is a decision of its own.

Nothing runs dotnet yet. `IExternalTools.Dotnet` resolves and no caller uses it.

`IExternalTools` resolves once and holds, so a change applies at the next launch, which
is what each description says and what the app already asked of someone installing git.

Measured on this machine, Linux: blank resolving to the git on PATH, an override actually
executed by `GitStatusReader` rather than PATH's git, an override pointing at nothing
falling back while reporting that it did, a relative path refused, blank accepted for a
tool and refused for the engine directory, and the engine default landing under the data
directory. The Windows half of `ExecutableFinder`, PATHEXT and the `.exe` name, is
unchanged by this and untested here.

Measured over a real workspace on this machine, 53 checks: layering and origin, a masked
team value, blank, out of range, unknown choice and wrong type all falling back with a
reason, reset removing and pruning, a batch landing, a broken team file drawn while its
write is refused and the file left byte for byte as it was, the personal layer still
writable beside it, and the read only and undeclared key guards.

## Application storage

What Workbench keeps for one person on one machine, outside any workspace, so it can
be read before a workspace is known. Three places, because they are backed up, roamed
and cleared differently. `IUserDirectories` says where each one is and is the only
thing that knows the OS layout. `ApplicationPaths` names the files under them.

| | Linux | Windows |
|---|---|---|
| Configuration | `$XDG_CONFIG_HOME/workbench` or `~/.config/workbench` | `%APPDATA%\Workbench`, roams |
| State | `$XDG_DATA_HOME/workbench` or `~/.local/share/workbench` | `%LOCALAPPDATA%\Workbench\State` |
| Cache | `$XDG_CACHE_HOME/workbench` or `~/.cache/workbench` | `%LOCALAPPDATA%\Workbench\Cache` |

An XDG variable holding a relative path is ignored, which the spec requires.

The application folder is lower case on Unix and keeps its written case on Windows,
which is what each platform does with its own directories. `IUserDirectories` folds it,
so a caller passes the application name once and never thinks about case.

State sits in the data directory by choice. The spec would put it under
`$XDG_STATE_HOME`, since a workspace list is a recently used list. It is here instead,
so a later directory for real user data would share this folder rather than take a
fourth one. Do not move it back without asking.

**`IApplicationSettings`** is configuration. Choices a person may edit by hand. It has
no layer to choose, so these can never be shared through a workspace's team config.

Typed readers sit over it rather than call sites naming keys. `IWindowSettings` is the
first, and it carries `window.nativeChrome`, covered under Window style.

**`IApplicationState`** is what the app remembers for itself. The list of workspaces,
which one is open, and window geometry when that arrives. The app writes it, a person
does not. Deleting the state directory resets Workbench without touching anything
anyone chose, which is the whole reason it is not in the config file.

Both take the same shape, scopes and all, and both are TOML through the same
`ISettingsDocumentStore`. They share `ScopedDocuments` and differ only in which
directory their files land in. `ISettings` is the read side of both, so read its name
as a typed read over a document rather than as a claim about settings.

The cache has a directory and `ApplicationPaths.CacheFileFor`, and no API beyond that.
Nothing caches anything yet, so the first thing that does picks its own shape.

## Platforms

Linux and Windows, 64 bit only. `Directory.Build.props` sets `RuntimeIdentifiers`
to `linux-x64` and `win-x64`.

Behavior that differs per OS lives in `Workbench.Core/Platform`, with one folder per
OS. Most of it sits behind `IPlatformServices`.

```
Platform/
  IPlatformServices.cs, PlatformKind.cs
  DesktopPlatform.cs                  shared behavior, subclasses supply Open
  WebAddress.cs, DirectoryLocation.cs
  IProcessRunner.cs, ProcessRunner.cs, ProcessRequest.cs, ProcessStartException.cs
  IExecutableFinder.cs, ExecutableFinder.cs
  Linux/                              LinuxPlatform, launcher resolution,
                                      user directories, path display
  Windows/                            WindowsPlatform, user directories, path display
```

`DesktopPlatform` holds everything shared and leaves one abstract member, `Open`,
which is the only thing that varies. Resolve `IPlatformServices` from the container
and never test the running OS at the call site.

Three IO services also vary, each with its own interface because each is asked for by
itself. They are registered together by `AddPlatformIO`, so a new one costs no extra
OS test.

- `IUserDirectories` the per user directories, covered under Application storage
- `IPathShortener` writes a path for display. `PathShortener` in `IO` holds the
  elision and a subclass per OS supplies the separator, the display form and the root
- `IPathRules` says whether two paths mean the same place, and whether one sits inside
  another. Only case sensitivity differs, so the subclasses are one line each

`AddPlatformIO` and `CreatePlatform`, both in `WorkbenchCoreServices`, are the only
places that test the running OS.

Targets are value objects. `WebAddress` accepts absolute http and https only, and
`DirectoryLocation` requires a rooted path. Parsing is the only way to make either,
so an unchecked target cannot reach the platform. This matters because the shell open
commands will run a local program given one. Whether a directory still exists is
checked when it opens, since the filesystem changes after a value is made.

Windows hands the target to the shell, so a replaced browser or file browser is
honored.

Linux assumes no particular distribution. `DesktopLauncherResolver` uses the first
launcher present on PATH, trying `xdg-open`, then `gio open`, then the KDE, XFCE,
MATE, and GNOME openers, then `wslview`. When none are installed it says so and lists
what it looked for. Add candidates there rather than in `LinuxPlatform`.

## Workspaces

A workspace is any folder. What makes it one is a `.workbench` directory, which
Workbench creates when the folder is added. `IWorkspaceRegistry` holds the list of
workspaces a person has added and which one is open, stored in application state so
it follows the user rather than any workspace.

Only the root path is stored. The name and the states are read from disk on every
refresh, so a renamed project or a folder that has gone missing shows up without
anyone maintaining a list.

**Name**, in order, from `IWorkspaceNameResolver`:

1. `workspace.name` in the workspace's team config
2. `config/name` from the first `project.godot` found under the folder, searched four
   levels deep, skipping `.git`, `.godot`, `node_modules` and similar. Note that
   `project.godot` is not TOML, since its keys contain slashes, so it is read by line.
3. the folder name

**States.** `IsLocal` means no repository, so there is no branch or history to show.
`IsMissing` means the folder is gone. A missing workspace is kept in the list rather
than dropped, so removing one is always a deliberate act.

**No nesting.** A folder inside a workspace that is already added is refused, and
`Add` throws `NestedWorkspaceException` naming the workspace it sits in. Settings are
found by walking up to the nearest `.workbench`, so a nested pair would leave a tool
started in the inner folder and one started in the outer folder disagreeing about
which workspace they are in. The check runs before anything is written, so a refused
folder is not left with a `.workbench` directory in it.

The launcher catches the exception and does nothing, so picking a nested folder is
silently ignored. That is deliberate and temporary. There is no error surface in the
launcher yet, and the catch exists only so a throw does not take the app down from an
async void handler. When a surface arrives, report the refusal there.

Adding the other way around, a folder that contains a workspace already added, is
still allowed. It makes the same overlap, so it is worth closing, but it was not asked
for and blocking it would refuse a legitimate move to a parent repository.

Roots are compared through `IPathRules`, not with string equality, because Windows
ignores case and Linux does not. A prefix test on its own would also read `game-tools`
as a child of `game`, so the separator is part of the test.

## Git

`Workbench.Core/Git` reads a repository by running git, and there is no library. Running
git works with whatever git the person has, honors their config, their credential helper
and their hooks, and cannot disagree with what they see in a terminal.

`IGitStatusReader` runs one command and parses it. `IGitFetcher` fetches.
`IGitStatusMonitor` follows one repository so the launcher's status bar stays true when
git is used from outside the app, which is where it is mostly used. All three come from
`AddWorkbenchGit`.

Porcelain v2, `git status --porcelain=v2 --branch --untracked-files=no`, is the format git
promises not to change between versions, which is the only reason parsing output is
defensible. Untracked files are excluded so an unignored build directory does not swamp
the count.

**`--no-optional-locks` is not optional.** Plain `git status` writes the index back to
refresh its stat cache. Measured: every run produces `Created index.lock`,
`Changed index.lock` and `Renamed index`. Anything watching the git directory then sees
its own read as a change and reads again, forever. VS Code passes `GIT_OPTIONAL_LOCKS=0`
for the same reason.

**Where git keeps a repository is asked, never guessed.** `rev-parse --absolute-git-dir
--git-common-dir` gives both, and `GitPlaces` holds them. `.git` under the workspace is
right only in the simplest case: a workspace can sit below the repository root, a worktree
and a submodule leave a file there instead of a folder, and `GIT_DIR` can point elsewhere
again.

**Updating runs unattended.** `GitUpdater` sets the variables that stop git waiting for a
person, since there is no terminal behind this to type into and it would wait until the
app closed. A credential helper already set up still works, because it answers without
asking. There is a 30 second limit as well, and cancelling kills the process tree, since
a network can accept a connection and then say nothing.

**Updating fetches, then takes the new commits only when they are free.** This is the one
thing in the app that writes to a person's repository, so the bar for doing it is high.
Every one of these has to hold, read **after** the fetch and never before it, since before
it the behind count is whatever it was last time anyone looked:

- the working tree and the index are clean
- the head is a branch and not a commit
- the branch tracks something
- the branch is behind, so there is a reason to
- the branch is not also ahead, so this is a straight line and not two histories

Then `--ff-only` on top, which is the guarantee rather than the check. Even with every count
stale, git will only move the branch pointer forward. It cannot merge, rebase, commit or
rewrite. It is a `merge --ff-only @{u}` rather than a second `git pull`, because the fetch
just above already brought everything down and a pull would go back to the network to learn
what it already knows.

Untracked files are the one thing the clean check does not cover, because the status read
excludes them. Git covers it instead: measured, an untracked file that an incoming commit
would overwrite makes the merge refuse, and the branch stays behind with the local file
untouched.

Measured across every case: clean and behind pulls, a modified file does not, a staged file
does not, diverged does not, a detached head does not, no upstream does not, already level
does not, and an untracked file in the way does not.

### Watching a repository

Modeled on what editors do and checked against two, the VS Code git extension and
SourceGit. Both watch the git directory rather than poll, both refuse to recurse into it,
both throw away lock files, and both debounce before running git.

**Watch a few named folders, never a tree.** `IDirectoryWatcher` has no recursive option
at all. A recursive watch costs one kernel handle per directory underneath, drawn on Linux
from a pool shared with every other application: measured on this repository, 242 against
1. A fetch or a repack then writes thousands of files under `objects` and says nothing
that `FETCH_HEAD` did not. VS Code recurses over the working tree instead, which it can
afford because the editor already runs one shared native watcher there. This app has no
such watcher, and the beat below does that job.

`GitPlaces.Watchable` is the list: the git directory, the common directory when this is a
worktree, and the reftable directory when there is one.

**A reftable repository writes nothing at the top level.** Git 2.45 added a second way to
store refs. A repository created with it leaves `.git/HEAD` a stub reading
`ref: refs/heads/.invalid` that never changes, and keeps every ref under `.git/reftable`.
Measured on git 2.55: a branch rename there changes nothing a watch on the git directory
alone would see. That folder is watched for exactly this.

**Lock files are ignored by name.** A `.lock` file is git reserving the right to write,
not git having written, and the write arrives under its own name a moment later.

**The beat is what the app relies on.** Every five seconds, and it catches the four things
no watch reports: a file edited in another editor, a push that moves only a remote ref, a
filesystem that reports nothing, and a watch that died because its folder was replaced. It
also puts dead watches back, which is why `IDirectoryWatcher.Watching` clears itself when
a watch dies. VS Code closes the push case with an extra watch on the upstream ref file,
which is more machinery than a status bar earns.

**Nothing runs while the app is not in front.** `IGitStatusMonitor.IsActive` parks the beat
and every watch driven read, and coming back reads at once. The launcher drives it from
`Activated` and `Deactivated`. VS Code parks its refresh the same way.

Reads are debounced 400ms, and a watch driven read cannot run more than once a second
whatever the debounce lets through.

**Switching workspaces replaces everything.** `Follow` stops the beat, drops the debounce,
disposes every watch, clears the status so it never describes a folder it is not
following, and reads the new folder at once. That read waits its turn rather than giving
up, since a read still running belongs to the folder nobody is looking at any more.
Following the folder already followed does nothing, which is why the launcher takes what
the monitor holds rather than blanking the strip itself.

Measured on this machine, both ref backends: a branch switch, a branch rename and a
staging all show up in about 400ms, a working tree edit within the beat, and an idle
repository produces no git processes at all. Counted from the kernel: a files repository
is 1 watch, a reftable one 2, a worktree 2, a folder that is not a repository 0, and
thirty switches back and forth leave the same 2 they started with.

## Window style

Every Workbench window draws its own title bar. This is the house style, so a new
window conforms rather than inventing its own frame.

Everything here lives in `Workbench.Ui`, so a tool gets the same window without copying
anything. **A window template must carry `<VisualLayerManager Name="PART_VisualLayerManager">`.**
`TopLevel` finds that part by name and installs the overlay and adorner layers into it,
and those are what adorners, tooltips and the light dismiss layer attach to. Without it a
window has no overlay layer, nothing is logged, and the symptom shows up much later as a
menu that will not close when a click lands inside the window.

Derive from `ui:ChromelessWindow` and put a `ui:WindowTitleBar` at the top of
the content. `Workbench.Ui/Themes/Controls/WindowChrome.axaml` supplies the frame, the
corner radius, the eight resize grips and the whole title bar, and
`WorkbenchTheme.axaml` already includes it, so a window writes one element:

```xml
<ui:WindowTitleBar Title="Workbench"
                   Icon="avares://Workbench/Assets/Icons/icon_64x64.png" />
```

`WindowTitleBar` owns the icon, the title, the caption buttons, the move drag and the
double click. Its content is whatever else the window wants in the chrome, such as a
menu or a toolbar, and it is empty by default. Do not hand write a title bar row, and
do not wire the gestures at the window, because both are already done here and doing
them twice fights the built in behaviour.

**A control in the bar keeps its own clicks.** A double tap bubbles, so one aimed at a
button in the chrome would otherwise reach the bar as well and maximise the window
behind it. Two quick clicks on a button are two clicks, not a gesture. The bar tests
whether anything between the source and itself is focusable: a button, a text box or a
caption button is a control and keeps the gesture, while a label, an icon, a border or a
panel is decoration, so dragging and double clicking the title still work.

The window's own icon, the one the desktop shows in the task bar, is set on the window
rather than in the base class, since it belongs to the app that is running:

```xml
Icon="avares://Workbench/Assets/Icons/icon_256x256.png"
```

The bar reads the window it sits in and mirrors it onto itself as classes, so every
selector in the theme matches on the bar alone rather than reaching across the window
and into a template. The classes are `nativeChrome`, `maximized`, `fixedSize`,
`noMinimize` and `inactive`. The window mirrors `inactive` onto itself as well, for the
one thing it draws rather than the bar, which is the outer edge.

**Inactive drops the chrome one tier.** The design states that without mapping it per
token, so stage 3 derived it: title `InkTitle` to `InkMuted`, caption glyphs
`InkCaption` to `InkSecondary`, outer edge `LineWindow` to `LineSeam`. The window icon
is a bitmap and cannot be retinted, so it gives up the same presence through opacity. A
hovered caption button comes back to full strength, so the close glyph is never dim on
its red flood.

**A dialog is a real window.** `ui:DialogWindow` is a `ChromelessWindow` that cannot be
resized or minimised, so its title bar keeps the close button alone. It centres on the
window that opened it and stays out of the task bar. There is no scrim behind it.
`ui:DialogFooter` is the row its actions sit in, on `SurfaceRoot` with a seam above.
The dialog lays out its own content between the two.

**A dialog's buttons carry a role, not a handler.** `ui:Dialog.Role` is `Accept` or
`Cancel`, and a roled button closes the dialog and answers for it, so
`await dialog.ShowDialog<bool>(owner)` says which was pressed and the caller wires nothing.
Closing any other way, the frame included, is a no. A button with no role is an ordinary
button, which is how a third answer such as Don't save is written.

**Enter and Escape are Avalonia's, not ours.** A role sets `IsDefault` or `IsCancel` and
the framework does the rest. Measured: Enter presses the accepting button while focus sits
in a text field, and Escape presses the cancelling one.

**`ui:Dialog.TakesFocus` says which control is ready**, and the accepting button is ready
when nothing says otherwise. Focus arrives as though tabbed to, so it wears the halo, since
a button that will answer the first Enter has to look like it. Marking the cancelling button
also takes Enter off the accepting one, because marking it is a statement that accepting is
the dangerous answer. Measured before that rule: a dialog opening with Cancel ready still
accepted on Enter, which is the opposite of what marking it asked for.

### The desktop can draw the frame instead

`window.nativeChrome` in `IWindowSettings` hands the frame to the desktop. It is global
and user only, so a workspace can never set it for everyone. It is read once when a
window is built and is not watched, so a change applies at the next launch.

`ChromelessWindow.UsesNativeChrome` carries it and keeps two mutually exclusive classes
in step. Style against the class, never against the property, and never assume the
drawn frame.

| Class | Frame | Caption buttons | Drag | Double click |
|---|---|---|---|---|
| `chromeless` | Workbench draws it, `WindowDecorations="None"` | shown | moves the window | toggles maximise |
| `nativeChrome` | the desktop draws it, `WindowDecorations="Full"` | hidden | nothing | nothing |

Under `nativeChrome` the row is ordinary content. It is not a title bar, because the
desktop already supplies one, so both gestures are disabled at their entry points,
`BeginMoveWindow` and `ToggleMaximizedFromTitleBar`.

**A bar with nothing of its own disappears.** Under `nativeChrome` the row is hidden
outright when the window put no content in it, since the desktop's title bar already
says everything it would have said. A bar that carries content stays, minus the caption
buttons, because that content has nowhere else to go. An empty `Panel` counts as
nothing, so a window can leave a container in place and still collapse.

Measured, the three cases:

| Frame | Content | Row | Caption buttons |
|---|---|---|---|
| drawn | either | 32px | shown |
| native | none | gone, height 0, template never realised | none |
| native | some | 32px | hidden |

The row is 32px including its seam, so a caption button is 32 wide by the room left
under the seam. That is not a fixed 32 by 32 square, and forcing it to be one would
overflow the seam. At 150 percent scaling it measures 30.67, because layout rounds the
1px seam up to 2 device pixels.

`ToggleMaximized` stays unguarded and available to code, so a window can still maximise
itself under either frame.

Behavior that is deliberate and should not be reported as missing: right click on the
title bar does nothing, and so does middle click. Both match the desktop defaults
recorded in `.claude/avalonia.md`.

**The window is larger than its visible frame.** It carries a 12px transparent gutter on
every side, `WindowShadowGutter`, and a `drop-shadow` effect falls into it. Avalonia 12
has no usable shadow API for a window that draws its own frame, so this is an app drawn
effect, copied from SourceGit. The reasons and the exact shape are in
`.claude/avalonia.md`.

The rule that keeps it looking right: **the shadow has no offset and its blur equals the
gutter**. Anything else runs past the window edge, gets clipped, and reads as a hard line
rather than a soft edge. Change one of the three and change all three.

Two things follow. A window's `Width` and `Height` include the gutter, so the launcher
asks for 964 by 724 to show the design's 940 by 700. Maximizing drops the gutter, which
drops the shadow with it, so the screen edge carries no transparent strip and no dark
band.

Anything clickable needs a `Background`, even `Transparent`. A control with no
background is not hit tested, so a look that only appears on `:pointerover` can never
be reached.

## Commands

```
dotnet build
dotnet run --project src/Workbench
```

A runtime identifier cannot be passed to the solution, only to a project.

```
dotnet publish src/Workbench/Workbench.csproj -r win-x64 --self-contained
dotnet publish src/Workbench/Workbench.csproj -r linux-x64 --self-contained
```

## Stack

- .NET 10, Avalonia 12.1.1, CommunityToolkit.Mvvm 8.4.2, Tomlyn 2.10.1,
  Humanizer.Core 3.0.10, Microsoft.Extensions.DependencyInjection 10.0.10
- Avalonia 12 changed a lot from 11 and most material online still describes 11.
  Read `.claude/avalonia.md` before working on views, styling or window chrome.
- Tomlyn 2.10 is a redesign. The old `Toml` static class is gone, replaced by
  `TomlSerializer` with a `System.Text.Json` style API.
- Humanizer writes the English a person reads: plurals that agree with a count, and a
  timestamp as how long ago it was. **It is `Humanizer.Core`, not `Humanizer`.** The meta
  package carries a satellite assembly per language, and nothing here is translated.
  `Workbench.Core` does not reference it, so it goes in each executable that needs it and
  the contract stays on Tomlyn alone.

## Status

Scaffolding, on the Slate design. The launcher lists the three placeholder tools the
registry holds and opening one reports that it is not built. No tool is implemented, and
no file format or Godot integration work has started.

The app is being moved to the Slate design, in the twelve stages under
`.claude/plans/`. **The numbers are the order**, and every stage depends only on lower
ones, so the plan runs straight through.

Stages 1 to 10 are done. `Workbench.Ui` carries the Slate
tokens, the type scale, the 49 icons, the window shell, the activity rail, every overlay
surface, the depth ramp, and the control themes built so far: five button kinds, the split
button, the dropdown button, the chip, the badge, the status pill, the progress bar, the
panel, the expander, the splitter, the collapsing sidebar, the text fields, the search
field, the checkbox, the radio, the toggle, the segmented row, the slider, the spinbox, the
combo box, the hyperlink, the list row, the tree, the tabs, the toast and the alert.

Stage 8 is done except the colour field, which waits on stage 13 because its swatch has
nothing to open until the picker exists.

The launcher is Slate throughout and holds no brush, hex, font size or radius of its own.
It is a shell now, a title bar over a rail and a page, with only the workspace page built.

**The launcher raises no toasts and holds no host, on purpose.** It has nothing transient
to report yet. The one slow thing it does, updating from git, reports on itself in the
status bar in place, which is rule 8 of that row and not something a toast should take
over. The gallery is where the toast service is wired to a composition root and exercised.

The git strip is real and reads the open workspace's repository. The engine strip is drawn
from `EngineViewModel.Placeholder` and every value in it is invented, which is the only
invented data in the app. It needs engine discovery, which is its own piece of work, and
`EngineViewModel` records what is already readable without it.

The settings schema is at step 3 of the six in `.claude/plans/settings-schema.md`. Core
has the schema types, the rules, the per layer read, the batched write, the remove, the
parse state as a value and the write refusal that goes with it. `window.nativeChrome` is
on a descriptor and `WindowSettings` reads its default from it. Nothing draws any of it
yet, and steps 4 to 6 are the window, the launcher's own schema and probes.

The rail's other two pages, Godot engines and Settings, are drawn and disabled. Both have
designs in the Claude Design project and neither has a stage yet. The rail itself carries
every state, including the two a pointer cannot reach on its own, an open page under the
cursor and an item that is open but disabled. All four are in the gallery.

## Open decisions

Do not assume any of these. Ask before building on one.

- Whether designer data moves to a custom format at all, and what that format is
- How the Godot side consumes it (EditorImportPlugin was the leading candidate,
  a runtime ResourceFormatLoader was the alternative)
- How references between data files are expressed (source file path was the
  leading candidate)

## Verified constraints about the Godot side

These were established by reading Godot 4.7.1 source at
`/home/jason/Projects/godot/godot-src-471` and by running probes. They are
expensive to rediscover.

- **GodotSharp cannot be used outside the engine.** Its native calls resolve
  against the host process, so with no engine present `new Resource()` and
  `new StringName("x")` segfault the process (exit 139), not throw. Pure managed
  types (`Vector3`, `Color`, `Aabb`, `Transform3D`, `Mathf`, `Variant` over
  primitives) work fine. Never reference GodotSharp from this app.
- **Type schemas can be read without the engine.** `MetadataLoadContext` over
  `.godot/mono/temp/bin/Debug/Slopworks.dll` loads cleanly and exposes `[Export]`
  hints, custom attributes with their constructor arguments, base types, and
  `ScriptPathAttribute`. Roughly 110 Resource derived types. Prefer consuming a
  committed schema manifest over reading the game's build output directly, so this
  app does not depend on the game being built.
- **UIDs can be read but should not be minted here.** `.godot/uid_cache.bin` is
  `u32 count` then per entry `{u64 id, u32 pathLen, utf8 path}`. The `uid://` text
  form is base 34 over the alphabet `a..y` then `0..8` (no `z`, no `9`). Godot mints
  UIDs itself on import, and `create_id_for_path` is seeded partly from the file's
  md5 so it is not stable across content edits.
- **Catalog addresses cannot be derived here.** `slopworks:machine.moldurr` style
  addresses come from a native GDExtension (`addons/resource_catalog/bin/*.so`),
  and the `CritGG.ResourceCatalog` NuGet package is only a GodotSharp facade over it.
  Already resolved entries are readable from the committed
  `addon_data/resource_catalog/collections/*.tres`, but new files have no entry
  until the plugin runs.
