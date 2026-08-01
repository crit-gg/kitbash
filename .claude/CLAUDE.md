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

- A container for extension methods must be static. `WorkbenchCoreServices` is the
  only one.
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

They use `TryAdd`, so calling several is safe and a caller can substitute any service
by registering its own first.

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
geometry come from `tools/icons/generate.py`, which reads `tools/icons/icons.txt` and the
licensed set outside the repo. **To add an icon, add its name to that list and rerun the
generator.** Never hand edit `Themes/Icons.axaml` or `Controls/IconGlyph.cs`. The outputs
are committed so a clean checkout builds without the set present, and
`generate.py --check` proves they have not drifted.

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
`Chip` and `StatusPill`. `Icon`, `ChromelessWindow`, `WindowTitleBar`, `DialogWindow` and
`DialogFooter` are ours for the same reason.

Theme the whole type rather than reaching past it. Where a control publishes settings for
a theme to drive, such as `ProgressBar.TemplateSettings`, use them and take the values it
computes. Holding a built in control to a number it does not compute means owning it.

One `ControlTheme` per type in `Workbench.Ui/Themes/Controls`, all merged by
`WorkbenchTheme.axaml`. A view names a kind and never a value.

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
everyone. Writes rewrite the file from the model and do not keep comments. There is
no file watching, so one process does not see another's write until it reloads.

`ISettingsDocumentStore` is the only place the file format lives. `SettingsDocument`
holds nested tables and no format specific types, so moving off TOML would touch one
class.

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
  Microsoft.Extensions.DependencyInjection 10.0.10
- Avalonia 12 changed a lot from 11 and most material online still describes 11.
  Read `.claude/avalonia.md` before working on views, styling or window chrome.
- Tomlyn 2.10 is a redesign. The old `Toml` static class is gone, replaced by
  `TomlSerializer` with a `System.Text.Json` style API.

## Status

Scaffolding, on the Slate design. The launcher lists the three placeholder tools the
registry holds and opening one reports that it is not built. No tool is implemented, and
no file format or Godot integration work has started.

The app is being moved to the Slate design, in the twelve stages under
`.claude/plans/`. **The numbers are the order**, and every stage depends only on lower
ones, so the plan runs straight through.

Stages 1 to 6 are done. `Workbench.Ui` carries the Slate tokens, the type scale, the 48
icons, the window shell, the activity rail, every overlay surface, and the control themes
built so far: five button kinds, the split button, the dropdown, the chip, the badge, the
status pill and the progress bar.

The launcher is Slate throughout and holds no brush, hex, font size or radius of its own.
It is a shell now, a title bar over a rail and a page, with only the workspace page built.

**Two bands the design shows are absent on purpose.** The engine strip needs to know which
Godot versions are installed and the git strip needs to read a repository, and this app
can do neither yet. Filling them with plausible numbers is what the no mock data rule
forbids, so they are not there. Wiring each is its own piece of work.

The rail's other two pages, Godot engines and Settings, are drawn and disabled. Both have
designs in the Claude Design project and neither has a stage yet.

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
