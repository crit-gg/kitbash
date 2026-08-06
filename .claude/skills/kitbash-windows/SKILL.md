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

`ChromelessWindow.UsesNativeChrome` carries it and keeps two mutually exclusive classes
in step. Style against the class, never against the property, and never assume the
drawn frame.

| Class | Frame | Caption buttons | Drag | Double click |
|---|---|---|---|---|
| `chromeless` | Kitbash draws it, `WindowDecorations="None"` | shown | moves the window | toggles maximise |
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
