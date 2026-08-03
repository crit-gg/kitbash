---
name: workbench-platform
description: "Workbench platform rules. The per OS services behind IPlatformServices, process running and detaching, executable lookup, user directories, path rules, and workspace discovery and registration. Read before touching the filesystem, the environment, a path or a process."
---

## Platforms

Linux and Windows, 64 bit only. `Directory.Build.props` sets `RuntimeIdentifiers`
to `linux-x64` and `win-x64`.

Behavior that differs per OS lives in `Workbench.Core/Platform`, with one folder per
OS. Most of it sits behind `IPlatformServices`.

```
Platform/
  IPlatformServices.cs, PlatformKind.cs
  DesktopPlatform.cs                  shared behavior, subclasses supply Open and Detach
  WebAddress.cs, DirectoryLocation.cs
  IProcessRunner.cs, ProcessRunner.cs, ProcessRequest.cs, ProcessStartException.cs
  IExecutableFinder.cs, ExecutableFinder.cs
  Linux/                              LinuxPlatform, launcher resolution,
                                      user directories, path display
  Windows/                            WindowsPlatform, user directories, path display
```

`DesktopPlatform` holds everything shared and leaves two abstract members, `Open` and
`Detach`. Resolve `IPlatformServices` from the container and never test the running OS
at the call site.

**A program that should outlive Workbench goes through `StartDetached`, not through
`IProcessRunner.Run`.** Not waiting for a process is not the same as detaching from it.
Measured on this machine: a child started by `Run` gets Workbench's own process group
and its session, so a signal aimed at Workbench is delivered to it as well. Its own
window closing is not that, and a child does survive it, but a terminal closing, ctrl C
and a logout all are. Under `setsid` the same signal leaves the child running.

**A cgroup is not escaped by any of this.** Neither `setsid` nor a new process group
leaves the cgroup a process was started in, so a desktop that tears down the app's scope
with `KillMode=control-group` takes the engine with it whatever this does. Detaching
covers signals, not containment.

Linux wraps the request in `setsid`, which is util-linux, looked up through
`IExecutableFinder` rather than assumed present. Without it the request goes as it is,
which still outlives a launcher that simply exits. Windows hands it to the shell, which
is how everything else there is opened, and the case that closes is a job object rather
than a process group. That half is reasoned rather than measured, since this machine is
Linux.

Godot's project manager is the one caller. An engine is a person's next few hours of
work and the launcher is a window they may well close, so the two do not share a fate.

Three IO services also vary, each with its own interface because each is asked for by
itself. They are registered together by `AddPlatformIO`, so a new one costs no extra
OS test.

- `IUserDirectories` the per user directories, covered by the `workbench-settings` skill
- `IPathShortener` writes a path for display. `PathShortener` in `IO` holds the
  elision and a subclass per OS supplies the separator, the display form and the root
- `IPathRules` says whether two paths mean the same place, and whether one sits inside
  another. Only case sensitivity differs, so the subclasses are one line each

`AddPlatformIO`, `AddEngineFiles` and `CreatePlatform`, all in `WorkbenchCoreServices`,
are the only places that test the running OS.

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
2. `config/name` from the first `project.godot` found under the folder, through
   `IGodotProjectReader`
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

