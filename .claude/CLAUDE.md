# Workbench

Cross platform desktop app hosting designer facing tools for the Slopworks Godot
project at `/home/jason/Projects/godot/slopworks/godot`.

The point of this app existing outside Godot: the Godot editor's inspector and
save/load behavior are awkward for authoring gameplay data, and designers should
be able to work without the editor open.

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

- `AddWorkbenchIO` filesystem and environment
- `AddWorkbenchPlatform` the services that differ per OS
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
src/Workbench/         the launcher app, Avalonia 12
src/tools/             one project per tool, empty until the first tool is named
```

**Workbench** is the launcher and is its own executable. `Workbench` is also the
project name prefix for anything above the tool level. Tools are separately named
products and are not prefixed.

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

## Platforms

Linux and Windows, 64 bit only. `Directory.Build.props` sets `RuntimeIdentifiers`
to `linux-x64` and `win-x64`.

Behavior that differs per OS goes behind `IPlatformServices` in
`Workbench.Core/Platform`, with one folder per OS.

```
Platform/
  IPlatformServices.cs, PlatformKind.cs
  DesktopPlatform.cs                  shared behavior, subclasses supply Open
  WebAddress.cs, DirectoryLocation.cs
  IProcessRunner.cs, ProcessRunner.cs, ProcessRequest.cs, ProcessStartException.cs
  IExecutableFinder.cs, ExecutableFinder.cs
  Linux/                              LinuxPlatform, launcher resolution
  Windows/                            WindowsPlatform
```

`DesktopPlatform` holds everything shared and leaves one abstract member, `Open`,
which is the only thing that varies. Resolve `IPlatformServices` from the container
and never test the running OS at the call site.

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
- Avalonia 12 notes: compiled bindings are on by default, so `x:DataType` is
  required on views and templates. Data annotations validation is off by default.
  `SystemDecorations` is now `WindowDecorations`. There is no `Avalonia.Diagnostics`
  package for 12.x.
- Tomlyn 2.10 is a redesign. The old `Toml` static class is gone, replaced by
  `TomlSerializer` with a `System.Text.Json` style API.

## Status

Scaffolding. The launcher lists three placeholder tools and opening one reports
that it is not built. The settings system is in place but is not yet wired into
the launcher. No tool is implemented, and no file format or Godot integration work
has started.

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
