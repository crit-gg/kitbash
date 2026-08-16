---
name: kitbash-platform
description: "Kitbash platform rules. The per OS services behind IPlatformServices, process running and detaching, executable lookup, user directories, path rules, and workspace discovery and registration. Read before touching the filesystem, the environment, a path or a process."
---

## Platforms

Linux and Windows on x64, macOS on Apple silicon. `Directory.Build.props` sets
`RuntimeIdentifiers` to `linux-x64`, `win-x64` and `osx-arm64`. There is no Intel Mac
package and no universal binary.

Behavior that differs per OS lives in `Kitbash.Core/Platform`, with one folder per
OS. Most of it sits behind `IPlatformServices`.

```
Platform/
  IPlatformServices.cs, PlatformKind.cs
  DesktopPlatform.cs                  shared behavior, subclasses supply Open and Detach
  WebAddress.cs, DirectoryLocation.cs
  IProcessRunner.cs, ProcessRunner.cs, ProcessRequest.cs, ProcessStartException.cs
  IExecutableFinder.cs, ExecutableFinder.cs
  UnixPathShortener.cs                path display, Linux and macOS both
  NoDesktopIntegration.cs             nothing to write, Windows and macOS both
  UnavailableSecretStore.cs           a machine whose keyring is not spoken to yet
  Linux/                              LinuxPlatform, launcher resolution,
                                      user directories, path rules
  Windows/                            WindowsPlatform, user directories, path rules
  MacOS/                              MacPlatform, user directories, path rules,
                                      opener discovery by bundle
```

`DesktopPlatform` holds everything shared and leaves two abstract members, `Open` and
`Detach`. Resolve `IPlatformServices` from the container and never test the running OS
at the call site.

**A shared implementation is named for the family, not for one member.** `UnixPathShortener`
is Linux and macOS, `NoDesktopIntegration` is Windows and macOS, and `UnixEngineFiles` was
already the precedent. A class named after one OS registered on another reads as a mistake
every time somebody finds it.

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
than a process group. That half is reasoned rather than measured.

**macOS ships no `setsid` binary, so it detaches by launching differently rather than by
wrapping.** A program inside an application bundle is rewritten to
`open -n -a <bundle> --args <arguments>`, which hands the launch to LaunchServices, so the
child belongs to launchd and was never ours. That is a stronger detach than `setsid` gives.
`-n` is required: without it a second `open` of a running app raises its window and drops
everything after `--args`, so opening a second project would do nothing.

**Three guards stop the rewrite, and each is load bearing.** `open` cannot set a working
directory, so a request naming one is left alone rather than silently losing it. An
environment overlay would land on `open` rather than on the program, so a request carrying
one is left alone too. And the program is checked with `IsExecutableFile` first, because
`open` reports a broken bundle in an exit code nothing reads, where starting the program
directly throws and becomes the `ProcessStartException` callers already handle.

Anything else runs unchanged, which is the same answer Linux gives when `setsid` is missing.
A GUI launch has no controlling terminal, so the signals `setsid` exists to escape do not
arise. The rewrite lives in `Detach` alone, so `ApplicationRestart` needed no edit: it builds
a request over `Environment.ProcessPath`, which inside a bundle is already the right shape.

Godot's project manager is the one caller. An engine is a person's next few hours of
work and the launcher is a window they may well close, so the two do not share a fate.

Three IO services also vary, each with its own interface because each is asked for by
itself. They are registered together by `AddPlatformIO`, so a new one costs no extra
OS test.

- `IUserDirectories` the per user directories, covered by the `kitbash-settings` skill
- `IPathShortener` writes a path for display. `PathShortener` in `IO` holds the
  elision and a subclass supplies the separator, the display form and the root. There are
  two, since `UnixPathShortener` answers for Linux and macOS both
- `IPathRules` says whether two paths mean the same place, and whether one sits inside
  another. Only case sensitivity differs, so the subclasses are one line each

**macOS splits these two, and that is the one place where treating it as a Linux is wrong.**
Display follows Unix, with a tilde and forward slashes. Identity follows Windows, because
APFS ships case insensitive and case preserving. A volume formatted case sensitive is the
case `MacPathRules` is wrong for, which is the safer of the two ways to be wrong.

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
version 2 of that interface. **The reply is a signal to the connection that asked, and the
portal destroys a request whose caller has gone**, so the connection has to outlive the call.
That is why this talks D-Bus itself through `Tmds.DBus.Protocol` rather than shelling out:
`gdbus call` returns the request handle and exits, which takes the pick down with it. The
package is already in the graph, since the Linux secret store brings it, so naming it in the
project file only pins the version.

The request is named with a `handle_token`, and the signal is matched on the path ending in
that token, so two picks at once cannot read each other's answer. A response code other than
zero is a person changing their mind, which comes back as no colour rather than as a failure.

`NoScreenColour` is Windows, which has no portal. The gesture there would be a window over a
capture of the desktop, which is Godot's own fallback and is not built. **macOS takes it too.**
It has a real answer in `NSColorSampler`, which needs no permission, but it needs AppKit
interop and the eyedropper is drawn only where a host supplies a picker that can pick, so
nothing on screen is wrong for its absence. Reading the screen any other way would mean the
screen recording permission, which an eyedropper does not warrant.

## A bundle launched from Finder has almost no PATH

**launchd gives a `.app` `PATH=/usr/bin:/bin:/usr/sbin:/sbin` and nothing else.** It is not the
login shell's PATH, and `/etc/paths.d` is read by `path_helper` from shell startup rather than
by launchd. So Homebrew at `/opt/homebrew/bin` and a `dotnet` symlinked into `/usr/local/bin`
are both invisible to `IExecutableFinder`, and `GodotBuildTool` quietly falls back to the
editor because it believes this machine has no dotnet.

**`IBundleEnvironment` is where the correction belongs when it is built.** `ExecutableFinder`
already reads the corrected `PATH` and `ProcessRunner` already merges the same overlay under
every request, so what is found is what runs, and it costs no new interface and no new OS
test. macOS registers `PlainEnvironment` today, which is the honest answer until the PATH a
real Finder launch gives has been measured rather than assumed.

**macOS also has a `git` on PATH that does not work.** `/usr/bin/git` is a Command Line Tools
shim, and on a machine without them it exists, passes `IsExecutableFile`, and opens an install
dialog when run.

**`IDesktopIntegration` makes the AppImage findable.** An AppImage is a file in Downloads
with no menu entry. `LinuxDesktopIntegration` writes `applications/kitbash.desktop` and
`icons/hicolor/256x256/apps/kitbash.png` under `XDG_DATA_HOME`, and rewrites whenever
`$APPIMAGE` does not match, so moving the file fixes the entry at the next launch. It reads
the variable itself rather than going through `IUserDirectories`, because the root it wants
is the data root and not Kitbash's folder inside it. The icon is `$APPDIR/.DirIcon`, the
specification's own name for it whatever `--icon` was called.

**An entry we did not write is never touched.** Ours carries `X-Kitbash-Entry=true` on its
last line, and a file without that mark stops `Install` before either the entry or the icon
is written. An AppImage manager names its entry after the app as well, so Gearlever writes
that same `kitbash.desktop`, and theirs holds keys ours does not. Overwriting one took
Kitbash out of Gearlever's list entirely.

**`TryExec` is there for the manager rather than for the desktop.** Gearlever lists an
installed AppImage only when the entry's `TryExec` names a file that exists, so an entry
without it is skipped. It is a path and not a command line, so it carries no quoting, while
`Exec` keeps the quoting the specification asks for.

**There is no `Remove`.** Velopack runs no uninstall hook on Linux and an AppImage has no
uninstaller, so nothing would call one. `NoDesktopIntegration` does nothing and answers for
Windows and macOS both, since `Setup.exe` writes the shortcut and the uninstall entry, and an
application bundle in Applications is already in Launchpad and Spotlight where it sits.

**`StartupWMClass` is deliberately absent** from the entry. A wrong one is worse than none,
and the value Avalonia actually sets has not been checked with `xprop WM_CLASS`.

`AddPlatformIO`, `AddEngineFiles`, `CreateOpenerFinder`, `CreatePlatform` and
`CreateSecretStore`, all in `KitbashCoreServices`, are the only places that test the running
OS. All five say the same sentence when they do not recognise it, from one `Unsupported`
constant, so they cannot drift apart.

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

## Opening a workspace elsewhere

`IWorkspaceOpeners` is the Open in button beside the engine strip: every editor, IDE and
terminal on this machine, plus the ones a person added. It is **not** `IExternalTools`,
which says where git and dotnet are, and the two must not be merged. Everything lives in
`Kitbash.Core/Platform/Openers/` with a finder per OS beside the other platform code.

Ported from SourceGit, and `.claude/plans/external-tools.md` records which file each piece
came from.

**Two caching lifetimes, because the halves go stale differently.** What is installed is
found once and held behind a gate, the reason `DesktopLauncherResolver` already gives: a
program is not installed while the app runs. What a person set is read every call, since
the settings window changes it while the launcher is open. What is inside the workspace is
never cached at all.

**Nothing gathers when the menu opens.** `LauncherViewModel` walks for solutions with the
engine strip, off the UI thread.

**The launcher can get out of the way once a tool has started.** `launcher.after.externalTool`
is the setting, and the `kitbash-settings` skill has it. It follows a program from the menu
and nothing else, so showing the workspace folder applies nothing, and neither does a start
that failed, which leaves the launcher up with its toast on screen.

**The menu is filled when the rows change, never on `Opening`.** Measured: a `MenuFlyout`
whose items are added from its own `Opening` handler opens empty, because the presenter has
already been built by then. `LauncherWindow` listens to `OpenInViewModel.PropertyChanged`
instead and refills whenever `Rows` moves.

**JetBrains Toolbox writes `launchCommand` absolute on Linux and relative on Windows.**
`Path.Combine` silently discards its first argument when the second is rooted, so it
happens to work and reads as though it concatenates. `Path.IsPathRooted` is tested first
instead. Toolbox also leaves an entry behind for an IDE that has been removed, so the
program is checked before it becomes a row.

**Every terminal found is offered, not the first one present.** That is the opposite of
`DesktopLauncherResolver`, and the difference is whether a person can tell. Nobody knows
whether `xdg-open` or `gio open` ran. Everybody notices being handed Konsole on a machine
where they use Ptyxis.

**Any found tool can be hidden, and only a found one.** `IHiddenOpeners` is `tools.hidden`
in the global config, a list of opener ids, and the Open in settings page draws a toggle
per detected tool over it. A tool a person added is removed from `tools.custom` instead, so
there is one way to get rid of each kind rather than two ways to get rid of one.

**The filter is in `WorkspaceOpeners`, never in a finder.** What is held is what this
machine has, so turning a tool back on costs no second detection, and the hidden list is
read on every call the way the custom list is. `ReadAsync` is what the menu offers and
`ReadAllAsync` is everything found, which only the page that hides them wants.

**An id nothing answers to is kept.** Uninstalling a tool and putting it back keeps the
answer, which is the rule `launcher.after.tools` already follows for a tool that is gone.

**The menu is gathered, so something has to say when to gather it again.**
`ISettingsWindows.Closed` is that, and `LauncherWindow` reads the menu again on it. Without
it a tool stays in the menu until the workspace changes, which is also true of a custom
tool being added.

**A terminal is told where it is exactly once.** On Linux and Windows that is
`WorkingDirectory`, and a path in argv would be read as a command to run, so
`TakesPathArgument` is false and a terminal needing its own flag carries it in
`FixedArguments`, such as `wt -d .`. **On macOS it is the opposite**, because every opener
there runs through `open`, which ignores the working directory entirely, so the folder is the
only way to say where a terminal opens and it reaches LaunchServices rather than a shell.
`OpenerFinderTests` asserts the invariant rather than either answer.

## Finding an opener on macOS

**Bundles are found by looking, not by PATH**, which is the launchd PATH rule above making
itself felt. `MacWorkspaceOpenerFinder` probes `/Applications`, `/Applications/Utilities`,
`/System/Applications`, `/System/Applications/Utilities` and `~/Applications`, and a bundle
counts as installed only when it holds a runnable program, so a folder left by an uninstall
is not offered. It needs neither `IExecutableFinder` nor `IProcessRunner`.

**A bundle cannot be `WorkspaceOpener.Program`**, since it is a directory and would fail
`IsExecutableFile`, which `OpenerFinderTests` pins. So every macOS opener names `/usr/bin/open`
and carries `-a <bundle>` in `FixedArguments`. **No `-n` here**, unlike `Detach`: `open -a Code
<folder>` asks the running editor for a new window, which is what a person wants, where `-n`
would start a second whole editor. The request detaches itself, since `open` is not inside a
bundle and `Detach` leaves it alone.

**There is no Visual Studio and no `vswhere`**, since Visual Studio for Mac is discontinued.
JetBrains Toolbox keeps its `state.json` under `~/Library/Application Support/JetBrains/Toolbox`,
and `JetBrainsToolbox` needed no change at all, because it already tests `Path.IsPathRooted`
before combining and so covers either spelling of `launchCommand`.

**Four terminal marks do not exist yet**, for Terminal, iTerm, Alacritty and Warp. A missing
mark draws no icon and still draws the row, so it blocks nothing.

**A custom tool's arguments are one token, `{workspace}`.** The template is split into argv
**before** the token is filled in, so a path with spaces stays one argument. Whitespace
separates and double quotes group. **There is no backslash escape**, because Windows and
Linux disagree about it and a Windows path is what gets pasted in. A blank template gives
the workspace alone and a template naming no token gets it appended, so `--new-window`
works without anybody learning the token.

**A registry read needs only `OperatingSystem.IsWindows()`.** `Microsoft.Win32.Registry` is
in the plain `net10.0` ref pack and in the shared framework, and its annotation carries no
version, so `WindowsWorkspaceOpenerFinder` needs none of the per runtime gating the secret
store has. Measured by publishing both runtimes from this Linux machine with
`TreatWarningsAsErrors` on. Do not copy the `#if` pattern here.

**Where a brand mark lives is the launcher's business.** `WorkspaceOpener.IconKey` is a
plain string such as `jetbrains/RD`, and `Kitbash/Views/ExternalToolIcons` turns it into an
asset. Core draws nothing and takes no Avalonia. A missing mark draws no icon and still
draws the row.

**Nothing detected takes the button away entirely**, which is SourceGit's answer too. The
folder alone is not what the button says it does, so a menu holding only that is not worth
opening. A single terminal is enough to keep it.

**The menu is one flat list and nothing in it opens a submenu.** A tool is one row, and it
opens the first thing it would rather have, or the workspace folder when it found none.
Rider and Visual Studio take `.sln` and `.slnx` at depth 4, and the VS Code family takes
`.code-workspace` at depth 2, which are SourceGit's own numbers.

**First means shallowest.** `WorkspaceFileFinder` reads a folder before it descends and
orders by name, so a solution at the root beats one in a subfolder. A workspace with several
is not offered the choice, which is the trade a flat menu makes.

**The whole of `WindowsWorkspaceOpenerFinder` is reasoned rather than measured**, since
this machine is Linux. The registry keys, the `vswhere` output and its UTF 8 encoding, the
Cursor path and the Git Bash derivation have never been executed.

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

| | Windows | Linux | macOS |
|---|---|---|---|
| Behind it | Credential Manager, via `Meziantou.Framework.Win32.CredentialManager` | freedesktop Secret Service, via `Ace4896.DBus.Services.Secrets` | nothing yet |
| Native dependency | none | none, it speaks D-Bus rather than binding libsecret | none |
| `Unavailable` | never, the store is part of the OS | no session bus, or no daemon answering | always, for now |

**macOS answers `Unavailable` deliberately.** `UnavailableSecretStore` says so through an
outcome the interface already names and every caller already handles, and nothing consumes
`ISecretStore` yet, since the GitHub login is the unbuilt step of the tool distribution plan.
What must not go back is the throw, since a resolve would take the app down.

**The Keychain is the eventual answer and `/usr/bin/security` is not.**
`add-generic-password -w <secret>` puts the secret in argv where any process can read it out of
`ps`, and that subcommand does not read a password from stdin, so `ProcessRequest.StandardInput`
cannot rescue it. `SecItemAdd`, `SecItemCopyMatching` and `SecItemDelete` over
`kSecClassGenericPassword` is the right shape, P/Invoked into Security.framework, which keeps
the no native dependency row the other two fill.

**It wants the app signed with a stable identity first.** A keychain ACL is keyed on the code
signature, so an ad hoc signed build changes identity on every rebuild and macOS asks a person
for permission on every launch. That ties this to signing rather than to this change.

**No third `#if` for it, now or later.** The two that exist are there because a package is per
OS, and Security.framework needs no package, so `[SupportedOSPlatform("macos")]` plus the
`OperatingSystem.IsMacOS()` guard at the one call site satisfies CA1416 on every runtime.
`UnavailableSecretStore` is never removed from the compile set either, which is why its branch
in `CreateSecretStore` sits outside both blocks.

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

Measured on a Linux machine, through a harness resolving the store from a real
container: a secret written, read back byte for byte, removed and read again as `NotFound`,
a remove of a key never written answering `NotFound`, the label reading
`Kitbash: kitbash-harness (probe)` in the keyring, and all three operations answering
`Unavailable` with `DBUS_SESSION_BUS_ADDRESS` unset. The gating was measured by publishing
both runtimes from this one Linux machine: `win-x64` carries the credential package and no
`DBus.Services.Secrets.dll`, and `linux-x64` the reverse. `Tmds.DBus.Protocol.dll` is in
both and always was, since `Avalonia.FreeDesktop` depends on it.

**The Windows store has never been executed.** Its target name, persistence, size refusal
and the 1168 mapping are reasoned rather than measured.

## Which machine is the one being looked at

**Development moved to an Apple silicon Mac, so Linux is now an untested platform too.**
The AppImage path, the desktop entry, the portal eyedropper and the Secret Service store
cannot be exercised from here at all, and everything the Linux sections above record as
measured was measured somewhere else. Windows was never the daily machine and still is not.

What has been run on macOS: the whole suite, 880 tests with none failing and 11 skipped,
`dotnet build` and `dotnet build -r osx-arm64` both clean under `TreatWarningsAsErrors`, and
the published launcher opening and reading the engine catalogue into `~/Library/Caches/Kitbash`.
The osx-arm64 apphost is ad hoc signed by the SDK and `codesign --verify` passes, which is
what Apple silicon requires of any binary at all.

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


### Making a workspace

`IWorkspaceMaker` is Create new workspace, and it is the only thing here that writes a
folder a person did not already have. It comes from `AddKitbashWorkspaceCreation`, which
is its own registration because it pulls in git and the Godot project writer and the
plain list does not want either.

**`Check` is the whole of the validation and it holds no words.** It answers a
`NewWorkspaceState` and the application says it in English, the same split
`GitCloneOutcome` already has. The rules follow `ProjectDialog::_validate_path` in the
Godot 4 editor, so a folder Godot would refuse is refused here, with one difference:
**a folder with files in it is an error rather than a warning**, so there is no confirm
step and no way to create a project over somebody else's. A dot file does not count as
content, which is Godot's rule and is what lets a repository be initialised first.

Godot's two extra refusals, the home directory and the editor's own directory, are not
written out. Neither is empty, so the not empty rule already refuses both, and a rule
that can never fire is a rule that goes stale.

**A folder name has to survive `get_safe_dir_name`.** `FolderNameFor` is Godot's, so
`: * ? " < > | / \` become a dash, `.` and `..` become `dot` and `twodots`, and a trailing
period goes. The set is applied on both platforms, so a folder made on Linux still opens
on Windows.

**Create folder is Godot's `create_dir`, not a Kitbash idea.** The path field holds the
folder the workspace goes in plus the safe name on the end, the name rewrites that last
segment while it is still the one the name wrote, toggling it off strips the segment and
remembers it, and browsing picks the folder above. All four are
`_update_target_auto_dir`, `_create_dir_toggled`, `_project_path_selected` and
`_browse_project_path`. The one case Godot cannot reach is a blank path, since its default
project path setting always holds one and `workspaces.directory` may be empty, and there
the name alone is put on whatever is picked.

**Everything is written before the workspace joins the list.** The folder, the project,
the ignore files, then `Add`, then the team config, then git. A failure part way leaves
nothing in the list, and cancelling the dialog leaves nothing at all, since the dialog
holds every answer and writes none of them.

**The name and the engine pin go in the team layer, not the personal one.** That is the
opposite of renaming, and the reason is that nothing is tracked yet and both answers were
given by whoever is making the workspace for everybody. A config that will not take them
is swallowed, since the folder name still names the workspace.

`IGitInitializer` runs `git init` and nothing else. The default branch name and the ref
backend are the person's own configuration. It answers false rather than throwing when
git is missing or refuses, because a workspace with no repository still works.
