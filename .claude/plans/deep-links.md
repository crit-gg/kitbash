# Deep links

`kitbash://` links. A page, a README or a chat message carries one, a person clicks it,
and the launcher shows what it names. Four verbs, and **not one of them clones, installs
or runs anything on its own**.

The rules that govern registration are in the `kitbash-platform` skill, the packaging half
is in `kitbash-updates`, and the settings page target is in `kitbash-settings`. This file
is the grammar, the record of what was built and the list of what could not be tested here.

## The grammar

Both shapes a desktop may hand over are taken, `kitbash://verb/rest` and `kitbash:verb/rest`,
since a browser normalises one into the other. The verb is folded to lower case. A path
segment is not, because a page id is camel case and a version has dots in it.

| Link | What happens |
|---|---|
| `kitbash://clone?repo=<address>` | the clone dialog opens with the address in it |
| `kitbash://settings/<pageId>` | the settings window opens on that page |
| `kitbash://engine/<version>` | the engines page opens with that build revealed |
| `kitbash://tool/<id>` | the workspace page opens with that tool's card scrolled to |
| `kitbash://tools/repository?add=<address>` | a dialog asks, and only then does the list gain it |
| `kitbash://engines/repository?add=<address>&name=<name>` | the same for an engine repository, which a workspace names by `<name>` |

Anything else raises a toast saying Kitbash does not know the link. **The link is never read
back out into the toast**, since it came from a web page.

`<pageId>` is a `SettingsPage.Id`. The launcher's are `window`, `externalTools`,
`customTools`, `godotEngines`, `engineRepositories`, `launcher`, `workspaces`,
`workspaceGodot`, `workspaceLinks`, `toolRepositories`, `updates` and `workspaceState`. An id the schema does not hold opens the
first page rather than nothing, since a link outlives the version somebody wrote it against.

`<version>` is an `EngineVersionPattern`, so `4`, `4.7`, `4.7.1` and `4.7.1-mono` all work
and the loosest match wins. `<id>` is a whole `ToolId` such as `github.foundry` or the plain
name a manifest gives.

## What each verb does, and what it does not

**Clone.** Fills `CloneWorkspaceViewModel.Address` and opens the dialog. The dialog still
has to be answered. Nothing reaches git until `GitRemote.TryParse` accepts the address,
which takes http, https, ssh and the scp form and refuses everything else, a local path and
anything starting with a dash included.

**Settings.** `ISettingsWindows.Open` takes a page id now. A window already open is moved to
the page rather than a second one being opened.

**Engine.** `EnginesViewModel.RevealForAsync` was split out of `InstallForAsync`. It opens
the card and scrolls to it and stops there. **A link does not install an engine**, because
that is a download of a gigabyte and a person's to start.

**Tool.** Switches to the workspace page, refreshes the list if it has never been read, and
scrolls the card into view. Nothing here has it raises a toast saying a repository offering
it has to be on the list first.

**Tool repository.** The only verb that writes anything, and the only one with a dialog of
its own. `AddRepositoryDialog` names the host and says plainly that installing a tool runs a
program from it. An address already on either list is said to be there and nothing is
written. The address goes through `WebAddress.Parse`, so http and https only.

**Engine repository.** The same dialog, `AddRepositoryDialog.ForEngines`, saying that a
workspace naming it installs its builds. Only a repository on github.com is taken. `name`
defaults to the repository's own name, and a name the global list already holds is refused
with a toast, since a workspace could not then say which it means. Written to the global
list alone. `.claude/plans/engine-repositories.md` has the rest.

## Registration

### Windows

`WindowsDesktopIntegration`, per user under `HKEY_CURRENT_USER`, so nothing needs elevation.

```
HKCU\Software\Classes\kitbash
  (Default)      = "URL:Kitbash Protocol"
  URL Protocol   = ""
HKCU\Software\Classes\kitbash\DefaultIcon
  (Default)      = "<exe>,0"
HKCU\Software\Classes\kitbash\shell\open\command
  (Default)      = "\"<exe>\" \"%1\""
```

The empty `URL Protocol` value is what makes the key a scheme rather than a file type. Its
absence is the usual reason a registration silently does nothing.

Written on every launch, and skipped when the command already matches, so moving or
reinstalling fixes itself and an ordinary launch writes nothing. A copy started as
`dotnet app.dll` registers nothing, which is the same answer Linux gives outside an AppImage.

`Microsoft.Win32.Registry` needed no package reference and no conditional compilation. It is
in the shared framework for plain `net10.0`, compiles for all three runtime identifiers and
ships in every self contained publish. This is unlike `WindowsSecretStore`, which needed
both.

**Uninstall takes the key back**, through `OnBeforeUninstallFastCallback` in `Program.cs`,
because Velopack deletes `%LocalAppData%\Kitbash` whole and a key left behind would name a
program that has gone. Velopack declares those hooks for Windows alone and CA1416 refuses the
call without a guard, so `Program.Main` holds the one `OperatingSystem.IsWindows()` test in
the launcher. There was no way to write it without one.

### Linux

`LinuxDesktopIntegration`, which already wrote the desktop entry. Two lines were added to it:

```
Exec="<appimage>" %u
MimeType=x-scheme-handler/kitbash;
```

`%u` is the field code for one url. Without it the desktop drops the link and Kitbash opens
with nothing to show. The trailing semicolon is required, since `MimeType` is a list.

Then `$XDG_CONFIG_HOME/mimeapps.list` gains
`x-scheme-handler/kitbash=kitbash.desktop` under `[Default Applications]`. Every other
section, key and comment in that file is left exactly as it was, and a run that changes
nothing writes nothing.

**`update-desktop-database` is deliberately not run.** It builds `mimeinfo.cache`, which is
consulted for the list of programs that can open a type and as a fallback default. An
explicit default in `mimeapps.list` is read first and is enough on its own, and skipping it
means no dependency on `desktop-file-utils` being installed.

### macOS

`Info.plist` declares `CFBundleURLTypes` and LaunchServices does the rest. No runtime code,
no uninstall step.

vpk writes that file, so `build/release.sh` hands it one. Three things had to change with it,
all measured against vpk 1.2.0 rather than assumed:

- **`--plist` copies the file in verbatim.** It merges nothing and substitutes nothing, so
  the whole plist is written by the script and the version is substituted there.
- **`--plist` and `--bundleId` are refused together**, so the identifier moved into the file.
- **Every key vpk wrote had to be kept**, `CFBundleIconFile` included, since vpk still puts
  the icns at `Contents/Resources/icon.icns` and nothing else names it.

`CFBundleShortVersionString` carries a fourth part where `CFBundleVersion` does not. That is
vpk's own shape and is copied rather than improved on. Velopack reads neither at runtime,
since it keeps its own `sq.version`.

## Delivery

**Windows and Linux start a second copy** with the link as an argument. There is no way for
either desktop to speak to the copy already running, so the handover is ours.
`Avalonia.X11` and `Avalonia.Win32` ship no `IActivatableLifetime`, and that was checked
rather than assumed.

`ISingleInstance` carries the link now. The pipe was one byte and is the message as UTF-8,
with the end of the stream as the end of the message, so **a copy running an older build,
which sends one byte and no length, is still understood** as a plain request to come forward.
Anything past 8 KiB is not sent and not read.

**macOS hands the link to the app that is already running.** `Application.TryGetFeature<IActivatableLifetime>()`
gives the event, `ProtocolActivatedEventArgs` carries the `Uri`, and the feature is null on
the other two.

**A link can arrive before there is a window**, on all three, since the launcher spends its
first seconds behind a splash asking the feed. `App._pending` holds it and `Open` follows it
once the launcher is up. The macOS handler is subscribed in `OnFrameworkInitializationCompleted`
before anything else for the same reason.

**A second copy now exits cleanly.** It used to call `desktop.Shutdown()` from inside
`OnFrameworkInitializationCompleted`, which shuts the dispatcher down before the main loop
starts and makes Avalonia throw on the way in. The process died with a core dump every time,
which nobody saw because the handover had already happened. Following a link is the common
way to be the second copy, so the shutdown is posted to the dispatcher instead and the
process exits 0. `SingleInstance.Dispose` was made idempotent at the same time.

## What was verified, and where

Development is on Linux. Everything below was run on this machine.

| Verified | How |
|---|---|
| the whole chain on Linux | two isolated copies, real windows: the second exited 0 and the first opened its settings window |
| cross process file locking on this filesystem | a standalone program, since the unit tests hold both instances in one process |
| the desktop entry, the mimeapps.list edit and its idempotence | `DesktopIntegrationTests`, over a real data root |
| the pipe handover, the empty message and the one byte case | `SingleInstanceTests`, over a real pipe |
| link parsing and every refusal | `DeepLinkTests` |
| the settings page target | `SettingsPageLinkTests`, the real window over a three page schema |
| verb routing | `DeepLinkRoutingTests`, the real window with no view model |
| the generated Info.plist | built a real bundle with it, cross compiling to osx, and read the scheme back out |
| `vpk [osx] pack` accepts `--plist` | `OsxPackCommand : OsxBundleCommand` by reflection over vpk 1.2.0 |

**Nothing was run on Windows or on a Mac.** The registry keys, the Velopack uninstall hook,
LaunchServices and the macOS activation event are all read from the specification and from
the packages, not measured.

## What is not built

- **A person using an AppImage manager gets no handler.** `LinuxDesktopIntegration` leaves an
  entry it did not write alone, and vpk offers no way to put a `MimeType` line into the entry
  inside the AppImage, where `--categories` is the only knob. A manager writing its entry
  under a different name is fine, since ours is then written beside it. Only the exact name
  collision loses.
- **The portable Windows build registers itself like any other copy.** Run it once from a
  folder that is later deleted and the scheme names nothing. The alternative was for it to
  register nothing at all, and registering the copy that ran is what Linux does.
- **Nothing emits a link yet.** There is no Copy link action anywhere in the app, so every
  link is one somebody typed. That is the next thing worth building and it is not here.
- **The other uses were considered and refused**, on purpose: a link to one record inside a
  tool, and a Godot editor plugin linking back. Neither is built and neither is planned here.
- **An OAuth redirect is not one of these and must not become one.** `github-login.md` chose
  device flow precisely because it needs no redirect uri, no loopback listener and no port to
  bind. A `kitbash://auth/github` callback would put all of that back.

## Security

A link comes from a web page, so every part of it is somebody else's.

- **A fixed verb list.** Anything not in it raises a toast and does nothing.
- **No path and no command ever comes out of a link.** A workspace is named by something
  already in the registry, an engine by a version pattern, a tool by an id, a page by an id.
  The only free text a link carries is a repository address, and both of those go through a
  parser that refuses anything but the schemes it names.
- **Nothing runs without a person.** No clone, no install, no script, no engine start.
  Adding a repository is the one thing that writes, and it has a dialog of its own.
- **The link is never echoed** into a toast or a log line a person reads.
