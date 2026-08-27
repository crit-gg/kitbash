---
name: kitbash-windows
description: "Kitbash window rules. ChromelessWindow, WindowTitleBar, DialogWindow and dialog roles, native versus drawn chrome, the shadow gutter, and keeping the UI thread free. Read before adding a window or dialog, or before doing work that touches a disk on the UI thread."
---

### The first window is not shown from OnFrameworkInitializationCompleted

The launcher checks for an update before it draws anything, so `MainWindow` is set and
shown later, from an async method. Two things that costs, both in `App`:

**`ShutdownMode` starts as `OnExplicitShutdown`** and goes back to `OnLastWindowClose`
once the launcher is up. The update dialog is a window, so under the ordinary rule its
closing would be a last window closing and would end the app before the launcher opened.

**A window that opens before the launcher has no owner.** `ui:DialogWindow` centres on
its owner, so one shown at startup sets `WindowStartupLocation` to `CenterScreen` and
`ShowInTaskbar` itself, and opens with `Show` plus an await on `Closed` rather than
`ShowDialog`.

**Every path has to end with a window.** An exception on the way to opening one leaves the
app running with nothing on screen and no way to reach it, which is worse than any failure
it was reporting. The startup method catches everything and opens the launcher afterwards.

### Keeping the UI thread free

Nothing that touches a disk, a process or a network belongs on the UI thread. A view model
gathers what it needs on the thread pool, returns one value, and only that value touches
bound properties. `LauncherViewModel.Read` and `Apply` are the shape to copy: `Read` is the
disk half and runs anywhere, `Apply` is the screen half and runs after the await. One load
at a time behind a semaphore, since two overlapping race on what a registry holds.

**Coming back the other way is `IUiDispatcher`.** A timer, a directory watch or any
background read raises its event on a thread pool thread, and a bound property touched from
there throws or draws nothing. `AddKitbashDispatcher` registers the Avalonia one, and a view
model takes the interface rather than reaching for `Dispatcher.UIThread` itself, so a test
can hand it one that runs the action there and then. `IGitStatusMonitor` is the case that
needs it, and both the launcher and Splice pair the two.

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

## Window style

Every Kitbash window draws its own title bar. This is the house style, so a new
window conforms rather than inventing its own frame.

Everything here lives in `Kitbash.Ui`, so a tool gets the same window without copying
anything. **A window template must carry `<VisualLayerManager Name="PART_VisualLayerManager">`.**
`TopLevel` finds that part by name and installs the overlay and adorner layers into it,
and those are what adorners, tooltips and the light dismiss layer attach to. Without it a
window has no overlay layer, nothing is logged, and the symptom shows up much later as a
menu that will not close when a click lands inside the window.

Derive from `ui:ChromelessWindow` and put a `ui:WindowTitleBar` at the top of
the content. `Kitbash.Ui/Themes/Controls/WindowChrome.axaml` supplies the frame, the
corner radius, the eight resize grips and the whole title bar, and
`KitbashTheme.axaml` already includes it, so a window writes one element:

```xml
<ui:WindowTitleBar Title="Kitbash"
                   Icon="avares://Kitbash/Assets/Icons/icon_64x64.png" />
```

**The frame is `:is(Window).chromeless`, and the `:is` is load bearing.** A bare type
selector matches the style key exactly, so `Window.chromeless` would reach `ChromelessWindow`,
which forces its style key back to `Window`, and nothing else. Dock's floating tool window
keys on its own type and would silently miss the frame.

**A window that cannot derive from `ChromelessWindow` wears the frame by setting the class.**
`ui:KitbashHostWindow` is the one that does. Anything taking it that way also needs
`ui:WindowResize.Grips`, which is what turns a press on one of the eight named borders into a
resize. `ChromelessWindow` sets it in its own constructor. The template is also keyed as
`WindowFrame` for a window that wants to point at it directly, through a `DynamicResource`,
since a `StaticResource` cannot reach another document's resources.

**`ChromelessWindow` is `Focusable`, and that is load bearing.** It is what drops a text
field's focus when a person clicks empty space, because Avalonia moves focus up from
whatever was pressed and gives up if it finds nothing focusable. A window that does not
derive from it loses the behaviour. See `.claude/avalonia.md` under Input.

`WindowTitleBar` owns the icon, the title, the caption buttons, the move drag and the
double click. Its content is whatever else the window wants in the chrome, such as a
menu or a toolbar, and it is empty by default. Do not hand write a title bar row, and
do not wire the gestures at the window, because both are already done here and doing
them twice fights the built in behaviour.

**`Version` is a readout after the title, and it is not the title.** Mono, one size
down, `InkMuted`, and it gives up a tier through opacity when the window is inactive,
because the title lands on `InkMuted` there and would otherwise match it. Blank collapses
the slot, which is every window that sets none.

It is a slot of its own rather than content, for two reasons. **Content decides whether
the bar survives native chrome**, so putting a version there would keep a row alive that
should collapse. And `Window.Title` is what the desktop and the task bar read, so the
version stays out of it: the launcher's task bar entry says Kitbash, not Kitbash
0.5.0.

**Under `nativeChrome` the version is not shown at all**, since the row collapses when it
carries no content. The settings window says it too, so it is not the only place.

**A control in the bar keeps its own clicks.** A double tap bubbles, so one aimed at a
button in the chrome would otherwise reach the bar as well and maximise the window
behind it. Two quick clicks on a button are two clicks, not a gesture. The bar tests
whether anything between the source and itself is focusable: a button, a text box or a
caption button is a control and keeps the gesture, while a label, an icon, a border or a
panel is decoration, so dragging and double clicking the title still work.

The window's own icon, the one the desktop shows in the task bar, is set on the window
rather than in the base class, since it belongs to the app that is running:

```xml
Icon="avares://Kitbash/Assets/Icons/icon_256x256.png"
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

**Open a dialog with `ShowFor`, not `ShowDialog`.** It copies the owner's frame onto the
dialog first, so a launcher wearing traffic lights does not open a dialog wearing Kitbash
caption buttons. Reading `Owner` inside the dialog cannot do this: a window is given its
owner after its first layout pass, which is already too late to change the template or the
size. `ShowDialog` still works and still leaves the dialog on the drawn frame.

**A dialog's buttons carry a role, not a handler.** `ui:Dialog.Role` is `Accept` or
`Cancel`, and a roled button closes the dialog and answers for it, so
`await dialog.ShowFor<bool>(owner)` says which was pressed and the caller wires nothing.
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

**Two dialogs are built and both are handed their words rather than a view model.**
`ui:ErrorDialog.For` is one thing failed and here is what the program said, with the words
in a mono well and a copy button hard left of the decision. `ui:ConfirmDialog.For` is one
question with two answers, and `ConfirmWeight.Grave` is the form for something that cannot
be undone: the accepting button goes red and cancelling becomes the ready one, so no
keypress can answer yes.

**Both call the generated `InitializeComponent`.** Defining one that calls
`AvaloniaXamlLoader.Load` leaves every named field null, which is the rule in
`.claude/avalonia.md` and which both of these broke until it was caught by a test.

### The desktop can draw the frame instead

`window.nativeChrome` in `IWindowSettings` hands the frame to the desktop. It is global
and user only, so a workspace can never set it for everyone. It is read once when a
window is built and is not watched, so a change applies at the next launch.

**What turning it off means is the desktop's answer, not the setting's.**
`WindowChromeRule` in Core says so, registered per OS in `AddPlatformIO`, and
`IWindowSettings.Chrome` is what a window reads. So no UI file learns what OS it is on.

| | `nativeChrome` off | on |
|---|---|---|
| Windows and Linux | `Drawn` | `Desktop` |
| macOS | `Overlay` | `Desktop` |

**`Drawn` is not offered on macOS, and that is correctness rather than taste.**
`BeginResizeDrag` is a no operation on the macOS backend and `WindowDecorations.None` drops
`NSWindowStyleMaskResizable`, so a window Kitbash framed itself could not be resized by our
grips or by the OS.

`ChromelessWindow.Chrome` carries the mode and keeps three mutually exclusive classes in
step. Style against the class, never against the property, and never assume the drawn frame.

| Class | Frame | Caption buttons | Drag | Double click |
|---|---|---|---|---|
| `chromeless` | Kitbash draws it, `WindowDecorations="None"` | shown | moves the window | toggles maximise |
| `nativeChrome` | the desktop draws it, `WindowDecorations="Full"` | hidden | nothing | nothing |
| `overlayChrome` | the desktop draws it over an extended client area | hidden | the platform's | the platform's |

Under `nativeChrome` the row is ordinary content. It is not a title bar, because the
desktop already supplies one, so both gestures are disabled at their entry points,
`BeginMoveWindow` and `ToggleMaximizedFromTitleBar`.

**Under `overlayChrome` the row is the title bar and the platform owns the gestures.**
`ExtendClientAreaToDecorationsHint` is true and `WindowDecorations` stays `Full`, since the
macOS backend zeroes its extended margins and hides the caption buttons for anything else.
The title bar already carries `WindowDecorationProperties.ElementRole="TitleBar"`, so
`ChromeHitTest` starts the real native move, which is why both entry points stay disabled
here too. Doing it ourselves as well would start two drags from one press.

**The caption inset is 80 and it is a constant, because Avalonia 12.1.1 exposes no way to
read it.** Measured on macOS 26.4 through `standardWindowButton`: the three buttons are 14
wide on 23 centres from x=9, so the last ends at 69, and 80 leaves the same 11 the drawn
frame leaves at its own edge. Two more measurements that happen to line up and are worth not
rediscovering: the system title bar is 32, exactly `HeightTitleBar`, and the buttons' vertical
centre is 16 from the top of the client area, exactly the centre of that row. **The inset is
dropped in full screen**, since macOS moves the buttons out of the window there.

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

**Only the drawn frame has a gutter, so `ChromelessWindow` takes it back out under the other
two.** Every window in the repository asks for its design size plus 24 on each axis, and
under a desktop drawn frame there is nothing for that 24 to pay for. `TakeBackTheGutter`
corrects `Width`, `Height`, `MinWidth` and `MinHeight` when the mode is set, which works
because the XAML has already run by then. A window sizing to its content has `NaN` there and
is left alone. **Keep the constant in `ChromelessWindow` and the thickness in the theme in
step**, since they are the same 12 said twice.

**The gutter is declared to the desktop, so snapping measures the frame.** Without it a
window manager tiles and maximizes against the whole window, and a snapped window sits 12
away from the edge it snapped to. `ChromelessWindow.Shadow` is `IWindowShadow` from Core,
handed over where the window is built the way `Chrome` is, and a window opened over an owner
takes its owner's. The number said is the window's own `Padding` in device pixels, so the
maximized style that drops the padding drops the declaration with it and nothing has to say
so twice. **X11 is the only desktop told anything**, and the `kitbash-platform` skill has
what each one does.

Anything clickable needs a `Background`, even `Transparent`. A control with no
background is not hit tested, so a look that only appears on `:pointerover` can never
be reached.


### The welcome window

`ui:ProjectsWindow` is the window an app opens with: one list of what it has opened
before. It is the library's, like the settings window, so a tool writes a class and one
line rather than a window. `.claude/plans/projects-window.md` has the whole of it and
every departure from the design.

**The library owns the store and the app owns what a project is.** `IRecentProjects` in
Core keeps paths, names, the order they were last opened in and whether each is still on
disk, and nothing else. It never looks inside a folder and never decides whether one
qualifies.

`IProjectKind` is the app's half: the words it uses, whether opening one closes the
window, any extra rail pages, what each row says beyond its name and path, and what New,
Open and opening do. `Describe` runs on the thread pool and may read a disk. An app that
throws there costs that row its chip, never the window its list.

**Extra pages follow the list down the rail.** A `ProjectPage` is a glyph, a word and a
control or view model, and the window draws the rail item, gives the pane over and knows
nothing else about it. A page is built once and kept, so coming back finds it as it was
left. Most apps return none. The cog is not a page: it opens a window and never stays
selected.

```csharp
services
    .AddKitbashRecentProjects(SettingsScope.ForTool("hoard"))
    .AddSingleton<IProjectKind, HoardProjects>()
    .AddKitbashProjectsWindow();
```

**The scope is the app's own**, so two tools never share a list. Register
`AddKitbashSettingsWindow` as well and the cog appears at the foot of the rail. Without
one the rail carries the list alone.

**Open it through `IProjectsWindows`**, which holds one window and brings the open one
forward. A null owner is an app with no other window yet, which centres on the screen and
shows in the task bar.

**A row that cannot be reached keeps its full name.** A greyed row reads as unimportant
when it is the one needing a decision, so only the path drops a tier and the chip carries
the alert. Opening one browses to relocate it rather than failing, whether the gesture was
the menu, a double click or Enter.

**Search filters and never sorts.** Sorting is its own dropdown with the current answer
written on it, so typing three letters never rearranges what is left.

### The splash window

`ui:SplashWindow` is the window an app shows while it starts, and it is the one window here
that depends on no styles at all. `.claude/plans/splash-window.md` has the whole of it. Read
it before changing anything in `SplashWindow.cs` or `SplashBackdrop.cs`.

**The host owns the lifecycle.** The library never opens one and never closes one.

Four rules, and breaking any of them makes the window draw nothing rather than draw badly,
under a host that has not loaded a theme yet:

- **It supplies its own `Template`.** A `Window` with no theme has no control template.
- **Nothing in it is a templated control.** No `Button`, no `ProgressBar`, no `ToolTip`, no
  `Viewbox`. `Border`, `Panel`, `Grid`, `StackPanel`, `Canvas`, `TextBlock`, `Image`, `Path`
  and `ContentPresenter` are the whole list of what draws with no styles loaded.
- **Every value is a literal.** No resource lookup of any kind. It is the one place in the
  app allowed to write a colour outside `Themes/Tokens.axaml`, and the fields are named
  after the tokens they copy, so a token change has to be repeated there.
- **Nothing is loaded from disk.** The art is geometry in `SplashBackdrop.Render` and the
  close glyph is `IconX` transcribed, not looked up.

**`FadeIn` is optional and zero is no fade at all.** Above zero it fades the whole card up,
the shadow with it, over that long from the moment the window opens. It is set before `Show`,
since the card is put on nothing the moment the value arrives. **There is no fade out**, since
a splash is replaced rather than dismissed.

**`ShowAsync` shows it and finishes when the fade has**, at once where there is none and where
the window is closed part way through. `Show` is unchanged and returns straight away.

**The close mark takes the pointer on press and answers on release**, only when the release
lands back on it. It is a `Border` rather than a `Button`, so that gesture is wired by hand
rather than inherited.

**The whole card drags the window.** There is no title bar, so the window handles
`PointerPressed` on the bubble route and calls `BeginMoveDrag`. The close mark marks its own
press handled, and a handled press never reaches that handler, which is the whole of how it
is excluded.

**It is not topmost**, since a window a person did not ask for should not sit over the one
they are using. A host that wants it pinned sets `Topmost` itself.

**The desktop never draws its frame, and `window.nativeChrome` does not apply to it.** That
setting reaches a window through `ChromelessWindow.UsesNativeChrome` alone, which this window
does not have, and `WindowDecorations` is put back to `None` if anything sets it otherwise.

**Progress is off until the host calls `Report`**, and it does both forms: `Progress` null
sweeps, and a value from 0 to 1 fills the track to that fraction over 180ms. The fill's
starting width is set before its transition exists, since a transition from an unset width
interpolates from `NaN` and the bar never draws. A filled bar also carries a faint
sheen, so it never reads as frozen. The pulse follows the row, the sweep follows the bar
having no fraction and the sheen follows it having one, so all three are cancelled separately
and a splash that never reports never draws a frame of animation. Each start is idempotent,
since `ApplyMotion` runs on every report and a restart would reset the motion each time.

**An `Effect` renders its whole subtree offscreen**, which is why the soft shadow on the text
is set per line rather than over a container. One over the progress row would take the pulsing
dot with it and repaint the row every frame.

**A person dismissing it before the app has a window of its own ends the app**, through
`IClassicDesktopStyleApplicationLifetime.Shutdown`, which ignores `ShutdownMode`. A host
closing it is a replacement and never ends anything. This is the one place in the window that
reaches for `Application.Current`.

**A host shows it as the temporary main window and swaps before closing it.** The lifetime
ends the app when the last window closes, so the real window has to be assigned to
`MainWindow` and shown before the splash is closed. `Kitbash.Gallery/App.axaml.cs` is the
worked example.

**Measured on this machine**, a published ReadyToRun build reaches a visible splash in about
530ms, roughly half of which is process start and the Avalonia platform coming up before any
of this code runs. Keeping the theme out of `App.Initialize` is worth about 150ms of that. The
gallery carries the probe that measures it and `.claude/plans/splash-window.md` has the table.

A host that wants the window up before its theme is parsed has to keep the theme out of
`App.Initialize` and add it afterwards, since `AvaloniaXamlLoader.Load` on an `App.axaml`
carrying `KitbashTheme` parses all of it before the first window exists. The gallery does
exactly that: no styles in `App.axaml`, all three added once the splash is on screen.
