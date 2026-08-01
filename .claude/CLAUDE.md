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
src/Workbench.Ui/      the look: tokens, type, control themes, fonts
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

That brings the tokens, the type scale and the icons. Nothing outside
`Themes/Tokens.axaml` writes a colour, a size or a radius. Names say what a value is for
rather than what it looks like, and two names may share a value when they are genuinely
different roles, which is noted in the file where it happens.

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

Derive from `ChromelessWindow` and put a `WindowTitleBar` at the top of the content.
`Themes/WindowChrome.axaml` supplies the frame, the corner radius, the eight resize
grips and the whole title bar, so a window writes one element:

```xml
<views:WindowTitleBar Title="Workbench"
                      Icon="avares://Workbench/Assets/Icons/icon_64x64.png" />
```

`WindowTitleBar` owns the icon, the title, the caption buttons, the move drag and the
double click. Its content is whatever else the window wants in the chrome, such as a
menu or a toolbar, and it is empty by default. Do not hand write a title bar row, and
do not wire the gestures at the window, because both are already done here and doing
them twice fights the built in behaviour.

The bar reads the window it sits in and mirrors it onto itself as classes, so every
selector in the theme matches on the bar alone rather than reaching across the window
and into a template. The classes are `nativeChrome`, `maximized` and `fixedSize`.

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
| drawn | either | 36px | shown |
| native | none | gone, height 0, template never realised | none |
| native | some | 36px | hidden |

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

Scaffolding. The launcher lists three placeholder tools and opening one reports
that it is not built. The settings system is in place but is not yet wired into
the launcher. No tool is implemented, and no file format or Godot integration work
has started.

The app is being moved to the Slate design, in the eleven stages under
`.claude/plans/`. Stage 1 is done: `Workbench.Ui` exists and carries the Slate tokens
and type scale. Nothing consumes them yet, so the launcher still looks as it did.

It keeps that look through `Themes/LegacyTokens.axaml`, which holds the old palette at
its old values under `Legacy` prefixed names. The prefix is deliberate. Nine of the old
names collide with a Slate token of the same name and a different value, and `AccentInk`
means opposite things in the two palettes, so merging them would have silently restyled
the launcher. **That file is temporary and stage 5 deletes it.** Do not use a `Legacy`
key in new work. A grep for the prefix lists everything stage 5 has to replace.

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
