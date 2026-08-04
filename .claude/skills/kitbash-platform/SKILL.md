---
name: kitbash-platform
description: "Kitbash platform rules. The per OS services behind IPlatformServices, process running and detaching, executable lookup, user directories, path rules, and workspace discovery and registration. Read before touching the filesystem, the environment, a path or a process."
---

## Platforms

Linux and Windows, 64 bit only. `Directory.Build.props` sets `RuntimeIdentifiers`
to `linux-x64` and `win-x64`.

Behavior that differs per OS lives in `Kitbash.Core/Platform`, with one folder per
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

**A program that should outlive Kitbash goes through `StartDetached`, not through
`IProcessRunner.Run`.** Not waiting for a process is not the same as detaching from it.
Measured on this machine: a child started by `Run` gets Kitbash's own process group
and its session, so a signal aimed at Kitbash is delivered to it as well. Its own
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

- `IUserDirectories` the per user directories, covered by the `kitbash-settings` skill
- `IPathShortener` writes a path for display. `PathShortener` in `IO` holds the
  elision and a subclass per OS supplies the separator, the display form and the root
- `IPathRules` says whether two paths mean the same place, and whether one sits inside
  another. Only case sensitivity differs, so the subclasses are one line each

Two more are registered there, both about the app being packaged rather than about IO.

**`IBundleEnvironment` takes the bundle back out of a child's environment.** Velopack's
AppImage `AppRun` puts `$APPDIR/usr/bin` on the front of `PATH`, so every program
Kitbash starts would search the bundle's 229 published files before the system. Read
from the generated `AppRun` rather than assumed: **`PATH` is the only variable it
exports.** The service strips a longer list anyway, since a hand built `.AppDir` passed to
`--packDir` is copied through untouched and could carry any `AppRun`.

`ProcessRunner` merges the overlay **underneath** a request's own, so `GitEnvironment.Unattended`
still wins on a key both set, and merging in the runner is what makes it cover
`GitStatusReader`, which passes no overlay at all. `ExecutableFinder` reads the corrected
`PATH` too, so what is found is what runs.

**Every caller runs somebody else's program today**, so the correction is applied to all of
them. When tools land, a tool started from our own bundle should keep the environment, and
that is the moment to add the distinction. `PlainEnvironment` is empty, for Windows and for
a Linux run outside a bundle, and `Outside` being empty is the honest answer for both.

**`IScreenColour` picks a colour off the screen, and the desktop runs the gesture.** A screen
is not ours to read: Wayland refuses it outright and hands the job to the portal. So the
interface is the whole gesture rather than a pixel read, `CanPick` says whether this desktop
has one at all, and a caller draws no eyedropper where there is none.

`LinuxScreenColour` calls `org.freedesktop.portal.Screenshot.PickColor`, which arrives in
version 2 of that interface. **The reply is a signal to the connection that asked**, and each
run of `gdbus` is its own connection, so the answer is read off `gdbus monitor` started before
the call and matched by the `handle_token` the call passes. Both go through `IProcessRunner`,
so no D-Bus library is taken and Core stays on Tomlyn alone. `gdbus` is probed for through
`IExecutableFinder` rather than assumed.

`NoScreenColour` is Windows, which has no portal. The gesture there would be a window over a
capture of the desktop, which is Godot's own fallback and is not built.

**`IDesktopIntegration` makes the AppImage findable.** An AppImage is a file in Downloads
with no menu entry. `LinuxDesktopIntegration` writes `applications/kitbash.desktop` and
`icons/hicolor/256x256/apps/kitbash.png` under `XDG_DATA_HOME`, and rewrites whenever
`$APPIMAGE` does not match, so moving the file fixes the entry at the next launch. It reads
the variable itself rather than going through `IUserDirectories`, because the root it wants
is the data root and not Kitbash's folder inside it. The icon is `$APPDIR/.DirIcon`, the
specification's own name for it whatever `--icon` was called.

**There is no `Remove`.** Velopack runs no uninstall hook on Linux and an AppImage has no
uninstaller, so nothing would call one. `WindowsDesktopIntegration` does nothing, since
`Setup.exe` writes the shortcut and the uninstall entry.

**`StartupWMClass` is deliberately absent** from the entry. A wrong one is worse than none,
and the value Avalonia actually sets has not been checked with `xprop WM_CLASS`.

`AddPlatformIO`, `AddEngineFiles`, `CreatePlatform` and `CreateSecretStore`, all in
`KitbashCoreServices`, are the only places that test the running OS.

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

## Secrets

`ISecretStore` keeps a credential for one person on one machine. Read, write and remove
against a `SecretKey`, which is a service and an account, parsed the way `WebAddress` is.
Registered by `AddKitbashSecrets`, which is its own method so a tool that wants a token
does not also take the process runner and the launcher lookup.

**A token never goes in settings.** The settings system is layered TOML and `config/` is
committed, the workspace scaffold writes every descriptor into it, and the settings window
draws each value with the line copyable. A secret has no business in any of that.

**`SecretResult` names four outcomes and `Unavailable` is the one that matters.** It means
this machine has no keyring, which is not the same as being signed out, and the two need
different words on screen. `NotFound` is nothing stored, `Failed` is a store that refused.

| | Windows | Linux |
|---|---|---|
| Behind it | Credential Manager, via `Meziantou.Framework.Win32.CredentialManager` | freedesktop Secret Service, via `Ace4896.DBus.Services.Secrets` |
| Native dependency | none | none, it speaks D-Bus rather than binding libsecret |
| `Unavailable` | never, the store is part of the OS | no session bus, or no daemon answering |

Windows keys on a target name alone, so both halves go into it as `Kitbash:service:account`,
and persistence is `LocalMachine` rather than `Enterprise` so a token does not roam to a
machine that cannot use it. A blob over 2560 bytes is refused before Windows answers 1783.
`CredentialManager.ReadCredential` gives null when nothing is stored, `DeleteCredential`
throws instead, so 1168 is mapped to `NotFound`.

The credential package declares `windows5.1.2600`, so the store carries that version and
`CreateSecretStore` tests `OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)`. Plain
`OperatingSystem.IsWindows` does not satisfy CA1416 and the build fails on it.

Linux reads `DBUS_SESSION_BUS_ADDRESS` through `IEnvironment` and answers `Unavailable`
without connecting when it is unset. A null collection or a null item is a keyring that
would not unlock, which is `Unavailable` too. It connects per call, since a token is read
rarely. The same attributes can match more than one item, so a read takes the first and a
remove deletes every match.

**The packages are gated in `Kitbash.Core.csproj` on `RuntimeIdentifier`, never on the
machine doing the build**, because `release.sh` publishes both runtimes from one machine.
An empty identifier is a plain build and takes both. A gated package means its store cannot
compile either, so the compile item goes with it and a constant each lets
`CreateSecretStore` name the type it has. Those two `#if` blocks are the only preprocessor
directives in the repository.

There is no fallback for a Linux machine with no Secret Service. It answers `Unavailable`
and stores nothing. An encrypted file protected by permissions alone was considered and
deliberately not built, since nothing has reported needing it.

**Restore lists both packages whatever the runtime**, because `RuntimeIdentifiers` is plural
in `Directory.Build.props` and restore covers every declared one. So `project.assets.json`
naming a package proves nothing. The build is where the condition applies, and the publish
output is the thing to check.

Measured on this machine, Linux, through a harness resolving the store from a real
container: a secret written, read back byte for byte, removed and read again as `NotFound`,
a remove of a key never written answering `NotFound`, the label reading
`Kitbash: kitbash-harness (probe)` in the keyring, and all three operations answering
`Unavailable` with `DBUS_SESSION_BUS_ADDRESS` unset. The gating was measured by publishing
both runtimes from this one Linux machine: `win-x64` carries the credential package and no
`DBus.Services.Secrets.dll`, and `linux-x64` the reverse. `Tmds.DBus.Protocol.dll` is in
both and always was, since `Avalonia.FreeDesktop` depends on it.

**The Windows store has never been executed**, since this machine is Linux. Its target name,
persistence, size refusal and the 1168 mapping are reasoned rather than measured.

## Workspaces

A workspace is any folder. What makes it one is a `.kitbash` directory, which
Kitbash creates when the folder is added. `IWorkspaceRegistry` holds the list of
workspaces a person has added and which one is open, stored in application state so
it follows the user rather than any workspace.

Only the root path is stored. The name and the states are read from disk on every
refresh, so a renamed project or a folder that has gone missing shows up without
anyone maintaining a list.

**Name**, in order, from `IWorkspaceNameResolver`:

1. `workspace.name` in this person's own user layer
2. `workspace.name` in the workspace's team config
3. `config/name` from the first `project.godot` found under the folder, through
   `IGodotProjectReader`
4. the folder name

**Renaming writes the user layer, never the team one.** `IWorkspaceRegistry.Rename` is
the launcher's Rename workspace, and the launcher's list is one person's. Writing the
team config would put a modified tracked file in the repository the status bar is
watching, so a rename made in a personal list would read as an unstaged change to
everybody. Blank removes the key and the four rules above decide again. The user layer
gets `.kitbash/.gitignore` written beside it, through `IWorkspaceScaffold`, so the
file never shows up as untracked either.

A folder that is not there is not renamed, since the name is a file inside it.

**States.** `IsLocal` means no repository, so there is no branch or history to show.
`IsMissing` means the folder is gone. A missing workspace is kept in the list rather
than dropped, so removing one is always a deliberate act.

**No nesting.** A folder inside a workspace that is already added is refused, and
`Add` throws `NestedWorkspaceException` naming the workspace it sits in. Settings are
found by walking up to the nearest `.kitbash`, so a nested pair would leave a tool
started in the inner folder and one started in the outer folder disagreeing about
which workspace they are in. The check runs before anything is written, so a refused
folder is not left with a `.kitbash` directory in it.

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

