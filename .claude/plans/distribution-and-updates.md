# Distribution and updates

How Kitbash and its tools reach a machine, and how they stay current, on Windows
and on any Linux distribution.

This was the research and the recommendation, written before the first tool existed,
because the answer changes how the projects are published and that is cheaper to decide
now.

## Status

**The launcher tier is built.** Velopack 1.2.0, the release script, the desktop entry and
the bundle environment. At start it checks its feed before drawing anything, and either
replaces itself over a progress dialog or opens on the version it already had. What is not
built: the single launcher guard, a preview channel, code signing and every part of the
tool system below.

**Five claims below were wrong and are corrected in place**, each marked `Corrected`.
They were written from documentation and reasoning, and the corrections come from
reading Velopack's source and from running a real update. Anything still unmarked is
still unverified.

## What has to be true

The constraints come from what the app already does, not from a wish list.

1. **Windows and Linux, any distribution, any desktop.** Same rule as everything else
   in this repo.
2. **A tool is a standalone program and nothing more.** It is its own process, it is not
   loaded in process, and **it is not required to be .NET.** The launcher discovers it,
   installs it, updates it and starts it. It does not host it.
3. **The launcher ships no list of tools.** It knows what is in its data directory and
   what a server offers. It is compiled against neither.
4. **The app runs the person's own programs.** It runs `git` from PATH. It hands a URL
   or a folder to the desktop opener. It reads project folders anywhere on disk and
   writes the three XDG directories. Any packaging that sandboxes the app breaks all of
   that.
5. **A person who is not a developer has to be able to install it and forget about it.**
   That is the whole point of a launcher.

## The shape of the answer

**Two tiers. Velopack installs and updates the launcher. The launcher installs and
updates tools.** This is the JetBrains Toolbox arrangement, and it is the right one here.

The alternative was one bundle carrying the launcher and every tool on one version
number. It is simpler and it was the first recommendation on this page. It loses on one
point that turns out to decide it.

**A single bundle makes every tool release a launcher release.** On Windows a launcher
update replaces the whole install folder and closes anything holding a file in it, so
shipping a one line fix to one tool means every person closes every tool they have open.
On Linux the same release is a fresh AppImage download for everyone. In the two tier
model a tool update writes one directory that nothing is reading, and nothing else has to
close. That is not a size optimisation, it is the difference between a fix a person takes
immediately and one they postpone.

Three more things follow from it, all in the same direction.

- **A person installs the tools they use.** A designer who wants one tool downloads one
  tool.
- **Adding a tool stops being a launcher release.** It becomes a row in a catalogue.
- **A tool need not be .NET, or ours.** A bundle would have forced everything through one
  build. Two tiers means the launcher installs a program, and what that program is written
  in stops being its business.

What the single bundle gave away for free, and now has to be built:

- **A written contract between two processes, and a version on it.** With tools in other
  languages there is no shared assembly to carry this. See below, it is the part most
  likely to be got wrong.
- **A catalogue, an installer and an uninstaller.** Perhaps three hundred lines, because
  the hard part of an updater is not present here. See below.

That trade is worth taking.

## Velopack, for the launcher

[Velopack](https://github.com/velopack/velopack) is the closest thing to a right answer.
MIT licensed, no fee, no subscription. The core is Rust. It is the successor to
Squirrel.Windows and Clowd.Squirrel, and the people who wrote those wrote it.

What it does:

| | Windows | Linux |
|---|---|---|
| Output | `Setup.exe`, plus an MSI when asked | one `.AppImage`, no installer |
| Install location | `%LocalAppData%\Kitbash`, no elevation | wherever the person put the file |
| Update feed | `releases.win.json` over static HTTP | `releases.linux.json`, same |
| Update method | swaps the whole `current` folder | replaces the AppImage in one rename |
| Deltas | yes, zstd binary patches per file | yes, after the first update |

The update feed is a JSON file next to the packages on any static host. GitHub Releases,
S3, Azure Blob, Backblaze B2, a plain web server, or Velopack's own hosted service. There
is no server component to run.

The integration is small. `VelopackApp.Build().Run()` has to be the first line of `Main`,
because Velopack reruns the main binary with hook arguments during install, update and
uninstall, and those hooks exit from inside `Run()`. Then `UpdateManager` gives
`CheckForUpdatesAsync`, `DownloadUpdatesAsync` and `ApplyUpdatesAndRestart`.

**Corrected. Velopack does cross compile between Windows and Linux.** An OS directive
goes before the verb, so `vpk [win] pack --runtime win-x64` runs here and
`vpk [linux] pack` runs on Windows. Only macOS needs a Mac, because it shells out to
`codesign` and `productbuild`. `dotnet publish -r win-x64` already runs anywhere too, so
one machine builds both releases and `build/release.sh` is one script rather than two CI
jobs.

Two limits stay. Signing a Windows package needs `signtool.exe`, so it needs Windows, and
nothing is being signed. And building a package is not testing it, so the Windows
artifact made here has not been run anywhere.

## The pack id is not the application name

**Added after the fact, and it is the worst thing this page nearly missed.**

Velopack installs to `%LocalAppData%\{packId}` and its uninstaller **removes that whole
directory**. `WindowsUserDirectories` already puts state at `%LOCALAPPDATA%\Kitbash\State`
and cache at `%LOCALAPPDATA%\Kitbash\Cache`. So a pack id of `Kitbash` would make the
install root their parent, and uninstalling would take the workspace list, every setting
the app wrote, and every installed Godot engine under `State\engines`, which is gigabytes
a person chose to download.

**The pack id is `Kitbash`.** It installs to `%LocalAppData%\Kitbash`, which is the parent
of `State` and `Cache`, so those two sit inside the install root and an uninstall removes
them. The name carries no company and that was the requirement. Velopack's own guidance is
to namespace a pack id, and this deliberately does not.

**So the hazard above is live on Windows and the fix is to move the user directories**,
not to rename the package. Nothing has been distributed, so nothing has been lost yet.

Two things follow. `--mainExe` becomes required rather than optional, since it otherwise
defaults to the pack id and the executable is named after the project. And the same rule
applies to anything added later: nothing a person owns may sit under the install root.

## Updates are off, and the feed is not a visible setting

`UpdateSettingsSchema.IsEnabled` is false. Nothing checks and nothing downloads.
Everything below is built and reachable, it simply does not run.

**Turning it on is two lines**: that property, and a real `DefaultFeed` pointing at the
bucket. It waits on the pipeline under Hosting, since a feed with nothing behind it would
have every copy checking a dead address on every launch.

It is a property rather than a const so the code it turns off does not read as unreachable,
which would fail the build under `TreatWarningsAsErrors`.

### `updates.feed` is a real key on no page

**The descriptor exists and the settings window never draws it.** Where releases come from
is the app's answer rather than a person's, so `DefaultFeed` is a constant compiled in, and
the Updates page shows the running version alone.

It is still an ordinary setting in every other way. It reads from a descriptor like all the
rest, out of `updates.feed` in the application settings file, so a copy can be pointed at a
feed by hand, which is how the whole path was tested. Measured: a value written into that
file by hand reads back through `SettingDescriptor.Read`, with no page involved.

Two things follow from being on no page. `ISettingsWriter` **refuses** the key, since it
guards against writing anything a page did not declare, so nothing can change it except a
person with an editor. And nothing announces it, since the file that lists every setting
from its descriptor is the workspace scaffold and this is an application setting, so the
key is written down here instead:

```toml
[updates]
feed = "https://..."   # or an absolute path to a folder
```

Everything measured below was measured with updates on, against a local feed and a
throttled HTTP server.

## Nothing about an update may cost somebody their app

The rule the launcher tier is built to. **Every path through startup ends with a window**,
because a copy that fails to update and shows nothing is worse than one that never tried.

- **`VelopackApp.Run` is wrapped.** A broken install state would otherwise stop the app
  before Avalonia loads. It carries on instead, unless the launch carries a `--veloapp-`
  hook argument, where opening a window in the middle of an install would be worse than
  stopping, so that one exits with a code.
- **`CheckAsync` never throws**, cancellation included.
- **A failed download closes the dialog and returns.** Measured with a package whose
  SHA256 was made not to match: the checksum failed, the launcher opened 1.9 seconds in,
  and the AppImage on disk was untouched at the old version.
- **Any failure in the update path is caught in `App.StartAsync`**, which opens the
  launcher afterwards whatever happened.

### The launcher waits for the check, so the check has a deadline

Nothing is drawn until the update is settled, which keeps a window from appearing and
being covered a moment later. That makes the check's duration a blank screen, so it is
bounded at three seconds.

**A cancellation token does not bound it on its own.**
`UpdateManager.CheckForUpdatesAsync` takes no token, so one handed to `Task.Run` only
stops it starting. Measured before this was understood: a server that accepted the
connection and never replied held the window back for over thirty seconds.

So `CheckAsync` stops waiting rather than cancelling, with `Task.WhenAny` against the
deadline, and reads the abandoned task's failure so it is never unobserved.

**`SimpleWebSource.Timeout` is a second, lower backstop, at fifteen seconds.** Velopack's
default is thirty minutes, which would leave an abandoned check holding a socket for half
an hour. Short is safe here, and the reason is worth keeping: the feed is read with
`GetStringAsync`, so the timeout bounds it end to end, while a package is read with
`HttpCompletionOption.ResponseHeadersRead`, so for the download it bounds only the wait
for the first response and never the body. Read from Velopack's `HttpClientFileDownloader`
rather than assumed.

Measured, launch to window, against a baseline start of about 1.7 seconds:

| Feed | Window |
|---|---|
| a server that accepts and never replies | 4.8s |
| nothing listening on the port | 1.8s |
| a host that does not resolve | 1.7s |
| a folder that is not there | 1.6s |

**The dialog is the only window while it runs**, so it opens with `Show` rather than
`ShowDialog`, centres on the screen, and sits in the task bar. `ShutdownMode` is
`OnExplicitShutdown` until the launcher is up, because the dialog closing would otherwise
be a last window closing and end the app before anything else opened.

## The four things that will actually bite

These are the reasons this page exists. Each one is a real defect in the obvious
implementation, and each has a defined answer.

### 1. A launcher update must not reach into a running tool

Velopack replaces the whole `current` directory on Windows. When a file in it is locked
it tries to kill whatever holds it, and if it cannot it shows a dialog offering to close
the processes. Velopack's own documentation says to apply updates explicitly rather than
let them auto apply while other processes are running.

**So nothing a tool touches may live inside the launcher's install directory.** Tools go
in the data directory instead, which is covered below and is the arrangement the two tier
model wanted anyway. Get that wrong and every launcher update is a fight with whatever a
person has open.

With tools outside it, a launcher update replaces only the launcher. Open tools are
separate processes reading separate directories, so they carry on, and the launcher exits
and comes back under them. That is the correct behaviour and it is worth confirming rather
than assuming.

**A tool never checks for its own update.** The launcher does it, for the launcher and for
every tool. One updater, one place, and a tool stays a tool.

### 2. An AppImage leaks its environment into every program it starts

**Corrected. It is one variable, not a class of them, and the variable is `PATH`.**

Velopack does not build a usual AppImage. The `AppRun` it generates, in
`src/vpk/Velopack.Packaging.Unix/Commands/LinuxPackCommandRunner.cs` and confirmed by
extracting a real one, exports `PATH` and nothing else. No `LD_LIBRARY_PATH`, no
`PYTHONHOME`, no `QT_PLUGIN_PATH`. That follows from a self contained .NET app, which
probes its own directory for native libraries rather than going through the loader path.
So the two reports linked below do not describe what happens here.

The `PATH` leak is real: `$APPDIR/usr/bin` goes on the front and holds the whole
published output, 229 files. Only two of them, `Kitbash` and `UpdateNix`, could shadow
a program at all, and neither collides with `git`, `dotnet`, `setsid` or an opener, so
this is a fix rather than an emergency.

`IBundleEnvironment` is what does it, and it strips a list of variables rather than
`PATH` alone, because a hand built `.AppDir` passed to `--packDir` is copied through
untouched and could carry any `AppRun`. `ProcessRunner` merges it under a request's own
overlay and `ExecutableFinder` reads the corrected `PATH`, so what is found is what runs.

The reasoning that turned out not to apply, kept because it is why the service exists:

An AppImage mounts itself and sets `LD_LIBRARY_PATH` and friends at the mount point so
its own binaries find their bundled libraries. Those variables are inherited by every
child process, including programs on the host that have nothing to do with the bundle.
The host program then loads our older libraries instead of its own and fails on a missing
symbol. This is a known AppImage design problem with a long trail of reports against
other applications. Two recent ones are worth reading, because both are this exact case:
GitHub's Copilot CLI AppImage broke `git` over HTTPS through a bundled `libnghttp2`, and
Buzz broke system `git`, `curl` and `python3` the same way.

**Kitbash runs `git` on a beat and hands folders and URLs to the desktop opener.** So
this app is squarely in the failure mode, on Linux, in the two places it talks to the
outside world.

**The fix goes in `ProcessRunner`, and only there.** Before starting a program that is not
ours, clear the variables the AppImage runtime set. `AppImage`, `APPDIR` and `OWD` say
whether we are inside one, and the runtime records the originals so they can be put back.
Programs inside our own bundle, which means the tools, keep the environment as it is.

That is one class, one place, and it is the same shape as every other platform difference
here. It has to be written before the first Linux release, not after a bug report.

### 3. Nothing on Linux knows the app exists

An AppImage is a file in the Downloads folder. There is no menu entry, no icon, no way
to start it other than finding the file and remembering it needs the executable bit. For
a tool a person runs once that is acceptable. For a launcher they open every day it is
not.

The usual answers have both gone quiet. AppImageLauncher development has largely stopped
since late 2025, and neither it nor `appimaged` is in the main repositories of Debian,
Ubuntu, Mint or Arch, so telling people to install one is telling them to go and find one.

**So Kitbash integrates itself on first run.** Write a `.desktop` file into
`$XDG_DATA_HOME/applications` with `Exec` pointing at `$APPIMAGE`, and the icon into the
hicolor theme under the same root. The file is a freedesktop specification we can read
rather than a guess.

`IDesktopIntegration` is that, and it is rewritten whenever `$APPIMAGE` does not match,
so moving the file fixes the entry at the next launch. Two details it turned out to need:
the root it wants is the data root itself rather than the folder Kitbash keeps its own
files in, so it reads `XDG_DATA_HOME` rather than going through `IUserDirectories`, and
the icon it copies is `$APPDIR/.DirIcon`, which is the specification's own name for it
whatever `--icon` was called.

**Corrected in one detail.** Velopack's `AppRun` does a little integration already: it
copies the icon into `~/.cache/thumbnails` and runs `xdg-icon-resource forceupdate` on
every launch. That is a file manager thumbnail, not a menu entry, so the entry is still
ours.

**There is no `Remove`.** Velopack runs no uninstall hook on Linux and an AppImage has no
uninstaller, so nothing would ever call one. Deleting the file is how a person uninstalls,
and the entry is left behind.

### 4. The update fails across filesystems

**Corrected. Velopack fixed this and there is nothing here to write.**

`apply_linux_impl.rs` runs `mv -f` rather than `fs::rename`, with a comment saying it is
because "rename fails cross-device". `mv` falls back to a copy and an unlink, so the
crossing is handled. The staging directory is `<packages>/VelopackTemp`, and packages on
Linux live under `/var/tmp/velopack/<id>/packages`, which is persistent disk rather than
the tmpfs `/tmp` usually is.

Measured here: an AppImage on `/tmp`, which is tmpfs, updated from a package staged on
`/var/tmp`, which is btrfs. Two filesystems, and it worked.

`VELOPACK_TEMP` exists and overrides the C# side's scratch directory, but it does not
move the packages directory, so it is not the lever this would have needed anyway.

One more thing worth knowing rather than fixing: **the first update on Linux is always a
full download.** Only the Windows installer ships a complete package, so there is nothing
for the first delta to diff against. Confirmed: the first update logged "There is no
local/base package available for this update, so delta updates will be disabled" and
fetched the full 48 MB, even though a delta was sitting in the feed beside it.

## Code signing: decided against

**Nothing is signed.** This is a deliberate choice, recorded here with what it costs so
nobody has to rediscover it.

On **Linux** it costs nothing at all. No desktop verifies an AppImage signature, so a
signature would be a file nothing reads.

On **Windows** it costs one warning at first install. `Setup.exe` arrives from a browser
with the mark of the web on it, SmartScreen finds no reputation for it, and the person
gets the blue "Windows protected your PC" box. Getting past it means clicking More info
and then Run anyway, which is two clicks and a moment of doubt. Windows Defender may also
quarantine it outright, which is the worse case, and Velopack's own documentation says
unsigned applications are more likely to be flagged.

Two things make that bearable, and they are the reason this decision is defensible rather
than merely cheap.

**Reputation was never going to accrue anyway.** SmartScreen builds reputation per
certificate for a signed application, and per file for an unsigned one. Every release is a
new file, so an unsigned application starts from zero on every version and never gets
quieter over time. Paying for a certificate is the only thing that changes that, and it
is not a warning that fades on its own. So the choice is between one warning forever and
paying, not between paying and waiting.

**Updates should not hit SmartScreen at all.** The warning is triggered by the mark of the
web, which a browser writes onto a file it downloads. Velopack's updater fetches packages
over HTTP itself and never involves a browser, so the downloaded packages should carry no
mark and never be checked. That makes the warning a once per person cost at first install
rather than a once per release cost. **This is expected behaviour rather than something
read in a document, so confirm it on the first Windows test.** If it turns out to be
wrong, a warning on every automatic update is bad enough to reopen the signing decision.

Per user install helps a little on its own account. `Setup.exe` writes to
`%LocalAppData%` and needs no elevation, so there is no administrator prompt on top of the
SmartScreen one.

If this ever ships to people outside the team, revisit it. Azure Artifact Signing, which
is what Trusted Signing is called now, is about ten dollars a month and carries SmartScreen
reputation immediately, and Velopack drives it through `--signTemplate`, so turning it on
later is a build argument rather than a rewrite. Signing from Linux needs a cross platform
tool such as JSign, since `--signParams` goes through `signtool.exe`.

## AppImage on Linux, kept without enthusiasm

**Nobody here wants to ship an AppImage.** It is kept because the alternatives cost more
than they save today, and this section exists so that judgement can be checked again
rather than assumed.

**Velopack builds nothing else on Linux.** `vpk [linux] pack` creates an AppImage and
that is the whole of its Linux output. There is no deb, no rpm, no tarball. So dropping
the format means dropping Velopack on Linux, and the launcher tier splits in two.

**Nothing surveyed covers both halves.** Every cross platform .NET updater is either
Windows only in practice, or stops before applying the update on Linux. See the entries
below for NetSparkle, Onova, Sewer56.Update and Qt Installer Framework. The gap is real
and it is not closing: deb and rpm support is Velopack issue 370, open since November 2024
with no plan attached.

**So the honest replacement was ours to write**: a tar.gz into
`$XDG_DATA_HOME/kitbash/app/<version>/` with a `current` symlink, the desktop entry we
already write, and an apply that extracts to `<version>.incoming`, renames, then renames a
new symlink over `current`, which is atomic on POSIX. A running file is never overwritten,
so the problem Velopack exists to solve does not arise on this side. **This is the same
machinery the tool installer needs**, described under The tool system below in exactly
those words, so building it is not wasted. JetBrains Toolbox works this way and is already
this page's reference for the two tier model.

It was not taken now because the launcher tier is finished and working, and because a hand
written installer is a thing to maintain forever. That trade changes the moment the tool
installer is built.

**What should reopen this:**

- **The tool installer landing.** Once a versioned directory and a symlink flip exist for
  tools, the launcher can use them too and the AppImage buys nothing.
- **The environment leak growing.** Today the generated `AppRun` exports `PATH` alone and
  `IBundleEnvironment` handles it. A Velopack release that starts exporting library paths
  would put this app back in the failure mode described under point 2.
- **Anyone reporting the AppImage as awkward.** Not knowing where the file went, losing it
  after a move, or the desktop entry failing on a session this machine could not test.
- **A second Linux format appearing in Velopack.** Issue 370.

`IBundleEnvironment` is the only code that exists purely because of the format. Everything
else, the desktop entry included, survives whatever replaces it.

## What was rejected, and why

**Flatpak.** The sandbox is the disqualifier, not the format. This app runs the person's
`git`, opens their file manager, and reads project folders anywhere on disk. Inside
Flatpak every one of those becomes `flatpak-spawn --host` plus a filesystem permission,
and a tool started as its own process is a second sandbox to reason about. Flathub would
also have to accept it. Flatpak is the right answer for a self contained application and
the wrong one for a developer tool that drives the host.

**Snap.** Same sandbox problem. The store backend is Canonical's and closed, so there is
no independent remote. Cold start is the slowest of the three.

**deb and rpm and AUR.** One package per distribution, forever, and no update path we
control. This is what "any distribution" was meant to avoid.

**MSIX.** Windows only, and it wants a certificate too. It solves nothing that
Velopack's installer does not.

**ClickOnce.** Windows only.

**winget.** Not an update mechanism. It is a discovery channel, and a good one, but the
manifest just points at `Setup.exe`. Worth adding later on top of Velopack rather than
instead of it.

**NetSparkle.** Genuinely cross platform, and the prebuilt Avalonia UI is current rather
than stale: `NetSparkleUpdater.UI.Avalonia` 4.0.0-preview, May 2026, depends on Avalonia
12.0.0 and ships `net10.0`. **It signs the appcast itself** with Ed25519, at
`appcast.xml.signature`, as well as each package, which Velopack does not do at all. It
stops at downloading and launching an installer. There is no installer on Linux, so the
apply and restart half is ours to write.

**Onova. Corrected: it is Windows only.** The reason recorded here was that automatic
restart does not work with Avalonia, and that issue is closed. Unpacking 2.6.13 settles
it: the package embeds `Onova.Updater.exe` with a config preferring .NET 3.5 or 4.x. The
Avalonia question never arises.

**Sewer56.Update.** 4.1.0, July 2026. Claims to run anywhere CoreCLR does and has CI on
Ubuntu. Updates only, no installer, and it comes out of the game mod world, so it is a
thin dependency to rest a launcher on.

**Qt Installer Framework.** The only thing found that installs and updates on both
Windows and Linux from a static HTTP repository, with a maintenance tool for the update
half. It is a Qt project, so it drags Qt into a .NET app for the installer alone, and it
is GPL or LGPL or commercial. Wrong shape for this.

**Squirrel.Windows and Clowd.Squirrel.** Windows only, and Velopack supersedes both.
There is a migration guide from either.

**Writing our own.** Checking a version file and downloading a zip is a day's work. Doing
an atomic swap of a running application on two platforms, with a rollback when it fails
part way, is not, and getting it wrong leaves people with a broken install and no way to
repair it from inside the app.

## The tool system

**Superseded by `tool-distribution.md`.** Sources are plural now and the first one is
GitHub releases, so the single catalogue described here is one later implementation
rather than the plan. What survives unchanged: the directory layout, the install being a
rename, the manifest being the contract, and the payload rules including the executable
bit. Read that page instead, and this one for the launcher tier alone.

The part that is ours to write. It is small, and the reason it is small is worth saying
plainly: **the hard part of an updater is replacing a running program with itself, and
that never happens here.** The launcher installs a tool, and the launcher is not the tool.
Velopack earns its place on the launcher for exactly the problem that is absent below.

### Where a tool lives

```
<data directory>/tools/<id>/<version>/
```

Which is `$XDG_DATA_HOME/kitbash/tools/` on Linux and `%LocalAppData%\Kitbash\tools\`
on Windows, both already resolved by `IUserDirectories`. `ApplicationPaths` needs a name
for it.

**Never inside the launcher's install directory.** On Windows that folder is replaced
wholesale by every launcher update. On Linux it could not be anyway, because an AppImage
is a read only mount whose path changes on every launch.

### Installing is writing a directory and flipping a value

Download to a temporary file, check the size and the SHA256 against the feed, extract to
`<version>.incoming`, rename that to `<version>`, then write the active version into
application state. Removing an old version is a separate step that can happen later, or
never, or at the next launch.

Every property that matters falls out of that. Nothing in use is ever overwritten.
Rollback is writing the previous version number back. Uninstall is deleting a directory.
A failed download leaves a temporary file and nothing else. There is no partially updated
state to recover from, because a version directory either exists whole or does not exist.

### The feed is static files on the same server

```
tools.json                        the catalogue: id, name, summary
tools/<id>/releases.json          versions, per platform payloads, notes
tools/<id>/<id>-<version>-<rid>.* the payload
```

A release entry names a platform, because a tool is not required to be portable. One entry
per platform it supports, plus room for a payload that works anywhere when the tool happens
to be written in something that does. Each entry carries the version, the url, the size,
the SHA256 and the manifest version, which is everything the launcher needs to decide
whether it can install a release before downloading it.

Do not invent more than that, and **do not reach for Velopack here.** `UpdateManager` does
accept an `IVelopackLocator`, so aiming it at a second install is technically possible,
but the documentation calls that a seam for testing. Building the tool system on it means
depending on something the project does not support, to avoid writing code that is
genuinely easy.

## The contract is the manifest and nothing else

The launcher installs a program and starts it. **What a tool does after that is the tool's
business**, and the contract should be small enough to say so.

Two things, and resisting a third is the discipline:

- **The manifest.** Its file name, its format and its fields: id, name, summary, icon,
  version, the executable per platform, and the manifest version below.
- **How the executable is started.** The arguments it gets, chiefly the workspace root, and
  the working directory it starts in.

That is the whole of it. Settings, workspace discovery and how a tool draws a window are
not in the contract. A .NET tool takes `Kitbash.Core` and `Kitbash.Ui` and gets all of
that for free, which is why they exist. Anything else reads a path from its arguments and
does whatever it likes. **The launcher never needs to know which.**

`Kitbash.Core` is therefore a library that makes writing a .NET tool easy, rather than
the contract every tool obeys. That is a smaller claim than it currently makes, and a
truer one.

`ITool`, `IToolActivation` and `IToolRegistry` stay useful as the launcher's own model of
an installed tool. Nothing in the launcher may assume a tool is managed code.

**This contradicts what `.claude/CLAUDE.md` currently says**, in two places: that
`Kitbash.Core` is the contract shared by every tool, and that tools are registered
explicitly in `App.BuildRegistry`. Both need amending when this is built.

### Versioning the manifest

The manifest carries its own version, a plain integer that goes up when its format breaks.
Separate from every product version on this page, because it is the only thing the launcher
has to understand before it can read anything else.

The launcher checks it before installing and again before starting.

- A tool needing a newer launcher: say so and offer the launcher update. This has to be
  first class, because it is the way a person otherwise gets stuck.
- A launcher past an installed tool: keep the tool, disable it, give the reason. Never
  delete something a person installed.

### Discovery, and the launcher ships no list

The launcher finds tools in two places and they answer different questions.

- **The data directory** says what is installed and can run. Scan `tools/*/` and read each
  manifest.
- **The catalogue on the server** says what can be installed. It is only ever a source of
  offers.

The installed side wins on every question about running. **A tool that has left the
catalogue keeps working**, because deleting a row on a server must never uninstall
anything. And a tool folder placed there by hand is simply a tool, which falls out of
manifest discovery for free and is worth keeping rather than defending against.

## The payload

**The launcher knows nothing about what is inside.** No runtime to share, no framework to
detect, no assumption that anything in there is managed. It extracts an archive, sets a
permission, and runs the file the manifest named. That is the whole of it, and keeping it
that dumb is what lets a tool be written in anything.

For a .NET tool that means publishing self contained, roughly a hundred megabytes. That is
the price of the rule and it is worth paying. There is no shared runtime, and there cannot
be one now, since the thing it would be shared with may not be a .NET program at all.

**No trimming and no Native AOT** for a .NET tool using Avalonia. Reflection roots have to
be protected by hand and the reward is not worth the class of bug it buys.

### The executable bit will be lost, silently

A zip does not really carry unix permissions. Info-ZIP and friends smuggle the file mode
into the upper sixteen bits of the external attributes field, and .NET does read it back
on Unix. **It is zero when the zip was written on Windows**, and .NET writing a zip on
Windows sets it to zero. So a Linux payload zipped by a Windows CI job extracts with no
executable bit and the tool cannot start, with nothing anywhere saying why.

Two things, both of them:

- **Use tar.gz for Unix payloads.** Tar carries the mode natively and does not depend on
  who wrote the archive.
- **Set the bit explicitly on the executable the manifest names**, after extraction, on
  Unix. It costs one line and it covers the tool that arrived some other way.

Tar does not fix helper binaries inside a payload that a zip flattened, which is the other
half of the reason to prefer it outright rather than to patch afterwards.

## Only one launcher at a time

Enforced in a release build, off under `DEBUG`.

This is not tidiness. **The launcher is the only writer of three things that have no
merge**, and two launchers means the second one silently undoes the first.

- **Application state.** The workspace list, which one is open, and the active version of
  every installed tool. Writes rewrite the file from the model, so the last writer wins and
  the other one's changes are simply gone.
- **The tool directory.** Two launchers installing the same tool both write
  `<version>.incoming` into the same place.
- **The launcher update.** Two calls to `DownloadUpdatesAsync` race. Velopack does guard
  this with a `.velopack_lock` and throws `AcquireLockFailedException`, so the second one
  fails rather than corrupts, but a person then sees an error for something they did not
  knowingly do twice.

One instance is the cheapest guard that covers all three.

### Take a file lock, not a named mutex

A named `Mutex` is the usual answer and it is a trap across these two platforms, because
**the prefix means opposite things on them.**

- On Linux, .NET needs `Global\` for a named mutex to be visible across processes at all,
  and the name should carry the user name, since the backing files sit in a shared
  directory under the system temp.
- On Windows, `Global\` means machine wide across sessions, which would stop a second
  person signed in to the same machine from opening the launcher at all.

So the same string is required on one and wrong on the other, and correcting it means
another test of the running OS.

**Hold an exclusive handle on a file instead.** `FileShare.None` is exclusive on both, so
it is one piece of code. The kernel drops the handle when the process dies, including on a
crash or a kill, which a PID file does not: a stale one locks a person out of their own
launcher until they find and delete it. And the scope is the path, which is already per
user, so nothing rests on a prefix whose meaning changes.

Where it goes is the usual question. `$XDG_RUNTIME_DIR` is the right answer on Linux,
being per user, per session, and cleared at logout. **It can be unset**, on a login that
is not going through systemd and over ssh, so fall back to the cache directory. On Windows
it belongs under local application data. **Never the configuration directory**, which roams
on Windows, so a lock file would follow a person to another machine.

`IUserDirectories` names three directories and this is a fourth kind of place. It belongs
there rather than worked out at a call site.

### The second instance should hand over, and might not manage it

Exiting silently is the wrong behaviour. Someone double clicked the icon and nothing
happened.

The handoff needs a channel. A named pipe works on both and is a unix domain socket
underneath on Linux. The second instance passes what it was asked to do, the first acts on
it and raises its window, and the second exits.

**Raising the window may not work on Wayland, and that is not a bug in this app.** A
Wayland client cannot take focus, it can only receive it. The compositor grants focus
against a single use activation token, which arrives as `XDG_ACTIVATION_TOKEN` in the
environment of whichever process the desktop actually started. So the second instance has
to pass that token along with everything else, and the first one uses it. Electron does
exactly this for its single instance lock.

When there is no token, the request is refused and the window stays where it is. That is
the defined answer rather than a failure to report. X11 has no such rule, so the same code
will look like it works in one session and not in another on the same machine. Test both.

### The exemption is cheap because a debug build has nothing to race on

`#if !DEBUG` on taking the lock. Two launchers side by side is normal while working on one.

Velopack throws `NotInstalledException` for a build that was not packaged and installed, so
under `dotnet run` the download and apply paths are already inert. What is left is
application state, which two debug launchers can still stamp on each other. That is a known
cost of the exemption, on the developer's own machine, and it is not worth guarding.

The case with a real race is a release build running beside a debug build. Worth not doing
rather than worth defending against.

### This says nothing about tools

Whether a tool allows one instance or many is the tool's business, and the manifest does not
ask. The launcher must not assume either way, which mostly means not assuming that starting
a tool which is already running will do nothing visible.

## Version numbers

**Semantic versions everywhere.** The launcher and every tool, one scheme, so a comparison
is the same comparison wherever it happens.

Velopack requires it of the launcher anyway. Three parts only, so `1.0.0` is fine,
`1.0.0-preview.1` is fine, `2026.8.1` is fine, and `1.0.0.0` is refused outright. Holding
tools to the same rule means one comparer in the launcher rather than one per tool.

The manifest version is the one thing on this page that is not a semantic version. It is a
plain integer describing a file format, not a product.

## Hosting: Backblaze B2, deployed from GitHub Actions

**Decided, and not built.** Releases go to a Backblaze B2 bucket, published by a GitHub
workflow that works out the version itself. **Until that exists, updates are off**, which
is the state the code is in now: `UpdateSettingsSchema.IsEnabled` is false, so nothing
checks, nothing downloads, and the settings window offers no feed. See Updates are off
below.

Three things this needs, none of them written:

**The workflow.** It publishes both runtimes and packs both, which one runner can do,
because `vpk` cross compiles between Windows and Linux. `build/release.sh` is that already
and takes a directory.

**Autoversioning.** The version is `<Version>` in `Directory.Build.props` today, and
`build/release.sh` reads it back out of MSBuild rather than repeating it. A workflow wants
it computed instead, from a tag or the run number, and passed to both `dotnet publish` and
`vpk pack --packVersion`. **Velopack takes three part semantic versions only**, so
whatever computes it has to produce one.

**The upload.** `vpk upload s3` is the command, and it works against B2 because B2 serves
an S3 compatible API. That is worth knowing precisely: there is no `vpk upload http`, so a
plain web server would have been a copy step of our own, and this is the reason to prefer
a bucket over one. `vpk download s3` goes at the front of the same job, so the previous
release is present and a delta gets built.

The bucket has to be readable without credentials, since `SimpleWebSource` sends none, and
everything under Four things the server has to get right below still applies to it.

### The previous decision, kept for its reasoning

Static files on a web server we run, at a URL that is not linked from anywhere. Backblaze
replaces the server and changes none of the reasoning below.

This is the case Velopack handles best. `SimpleWebSource` is what a plain URL passed to
`UpdateManager` resolves to, and it fetches `<base>/releases.{channel}.json` and then the
packages beside it. There is no server component and no API. Anything that serves files
will do.

Both feeds and the tool catalogue sit under one base URL:

```
<base>/releases.win.json          the launcher, Windows
<base>/releases.linux.json        the launcher, Linux
<base>/*.nupkg, Setup.exe, *.AppImage
<base>/tools.json                 the tool catalogue
<base>/tools/<id>/...             one folder per tool
```

**Publishing is a copy.** There is no upload provider for a plain web server. `vpk`
deploys to GitHub, Gitea, S3 and Azure Blob and nothing else, so the release step is
`vpk download http --url <base>` to fetch the previous release so a delta can be built,
then `vpk pack`, then `rsync` or `scp` the output directory up. Same on both operating
systems, and it is a handful of lines in CI.

### Four things the server has to get right

**HTTPS, with a certificate that validates.** Nothing is signed, so the feed is the entire
chain of trust. Velopack checks the size and hash of every package against the feed and
throws `ChecksumFailedException` when they disagree, which catches a tampered package and
does nothing at all about a tampered feed. TLS is the only thing protecting the feed. Over
plain HTTP anyone who can answer for that hostname can hand every installation a program
of their choosing, and it will install it and run it. This is worth stating flatly because
leaving HTTP enabled on an unlisted directory nobody visits feels harmless and is not.

**Do not cache the feed. Do cache the packages.** A CDN or a long cache lifetime on
`releases.*.json` hides new releases for as long as it lasts, and the symptom is that
updates quietly stop happening for some people. Package file names carry their version and
never change content, so those can be cached for as long as you like.

**Directory listing off.** It is the only thing standing in for access control.

**Serve bytes unchanged.** A proxy that recompresses or rewrites a response breaks the
hash check, and the failure looks like a corrupt download rather than a misconfigured
server.

### Hidden is obscurity and nothing else

**The base URL is compiled into the application, so everyone who has a copy has the URL.**
Treat that directory as public, because it is. Unlisted keeps releases off a search engine
and out of the way, which is all that was asked of it. It is not access control, so do not
later put something in that directory on the assumption that nobody will find it.

## Settled

- **Two tiers.** Velopack for the launcher, our own installer for tools.
- **A tool is a standalone program in any language.** The launcher discovers, installs,
  updates and starts it, and hosts nothing.
- **The launcher ships no list of tools.** The data directory and the server's catalogue
  are the only two sources.
- **The contract is the manifest and how the executable is started.** Nothing else.
  `Kitbash.Core` is a library for writing .NET tools, not the contract.
- **Semantic versions everywhere**, launcher and tools alike.
- **One launcher at a time**, in a release build only.
- **Our own server, unlisted, over HTTPS**, for the launcher's own feed. No GitHub
  Releases, no Velopack Flow. A tool comes from somewhere else entirely, and
  `tool-distribution.md` says where.
- **Nothing is signed.** One SmartScreen warning at first install, on purpose.
- **AppImage on Linux, for now.** Kept because Velopack builds nothing else there and
  nothing surveyed covers both operating systems. Revisit when the tool installer lands.

## Open questions

Do not build on an assumption for any of these.

- **Can a tool be started without the launcher?** Workspace discovery currently assumes a
  tool can be launched from a subdirectory on its own. If that stays true, each installed
  tool needs its own shortcut or desktop entry, written by the launcher at install and
  removed at uninstall, pointing into a versioned directory that moves on every update.
  That is a real piece of work and it is easy to miss when planning this.
- **What happens to a tool's settings when it is uninstalled?** Workspace settings under
  `.kitbash/` belong to the workspace and should survive. Application state for that
  tool probably should not. Decide before the first uninstall exists.
- **Does the launcher offer a tool it has no payload for?** A tool with a Linux build and
  no Windows one will exist. Showing it and refusing it is friendlier than hiding it, and
  either way it is a decision rather than an accident.
- **Per user or per machine on Windows.** Per user is the default, needs no administrator,
  and is almost certainly right. Per machine needs the MSI and an administrator.
- **One channel or two.** A preview channel is nearly free in Velopack and it is the only
  way to test the update path other than in production.

## The smallest thing that proves it

Steps 1 to 4 are the launcher tier and are done. What was run, on this machine, Linux:

1. **Done.** `build/release.sh` publishes self contained and packs a 49 MB AppImage. It
   runs.
2. **Done, and the answer changed the fix.** The generated `AppRun` was extracted and
   read: it exports `PATH` alone. See the correction under point 2 above. The scrub and
   the merge were then measured over the real `IBundleEnvironment` and `ProcessRunner`,
   twelve checks including a child process printing the `PATH` it was actually given, the
   request overlay still winning over the bundle's, a wholly bundled variable being unset,
   and a sibling directory whose name merely starts the same being kept.
3. **Done, and it crossed a filesystem on the first try.** 0.1.0 on tmpfs updated itself
   to 0.2.0 from a local directory feed, staging on btrfs. The modal drew the download,
   the app restarted, and the new process reported 0.2.0 and then correctly found no
   further update. A missing folder, an unreachable address, an `ftp` scheme and a
   relative path each left the window usable with no dialog.
4. **Half done.** The entry and the icon land under `~/.local/share` and the `Exec` line
   follows the AppImage when it moves. **Not checked: how it looks in a menu on two
   different desktops**, and whether the window groups under that icon, which is what
   `StartupWMClass` is for and why the entry does not carry one yet.

Two more that could not be run here. Running `git` from inside the AppImage against a real
workspace needs somebody at the window, and cancelling the modal part way through needs a
download slow enough to interrupt, which a local folder is not.

5. **Put a shell script in `$XDG_DATA_HOME/kitbash/tools/hello/1.0.0/` with a manifest
   beside it**, and have the launcher find it, list it and start it. No feed, no download,
   no installer, and deliberately not a .NET program. If the launcher can run a shell
   script it can run anything, and if it cannot then something in the design assumed
   managed code. This is the whole tool model on trial for the price of a directory.
6. Do the same with the gallery, which is a real .NET application, to see what a hundred
   megabyte payload actually feels like to install.
7. Then put both behind a `tools.json` on the server and let the launcher install them,
   from a tar.gz built on Linux and a zip built on Windows, so the permission trap gets
   hit here rather than by somebody else.

Steps 2 and 3 decide whether the Linux half survives. **Step 5 is the important one**, and
it is deliberately the cheapest. A shell script proves the launcher installs and starts a
program rather than hosting a .NET one, which is the claim everything else here rests on.

**On Windows, the one thing to check beyond the obvious** is whether an automatic update
triggers SmartScreen. The reasoning under Code signing says it should not. If it does,
that decision needs reopening.

## What could not be tested here

This machine is Linux. The Linux claims have now been run rather than read, and the
corrections above are what that turned up.

The Windows package **is built here**, since `vpk [win] pack` cross compiles: `Setup.exe`,
a portable zip, the nupkg and `releases.win.json` all come out of `build/release.sh`. None
of it has been executed. So these are still unwatched:

- that `Setup.exe` installs to `%LocalAppData%\Kitbash` without elevation
- **what uninstalling actually removes**, now that the install root is the parent of
  `State` and `Cache`
- **whether an automatic update triggers SmartScreen.** The reasoning under Code signing
  says it should not, because Velopack fetches over HTTP itself and never involves a
  browser, so no mark of the web is written. That is reasoning, not something anyone has
  watched. A warning on every update would be bad enough to reopen the signing decision.
- everything about the per machine MSI

## Sources

- [Velopack](https://github.com/velopack/velopack) and its docs:
  [Linux](https://docs.velopack.io/packaging/operating-systems/linux),
  [Windows](https://docs.velopack.io/packaging/operating-systems/windows),
  [integrating](https://docs.velopack.io/integrating/overview),
  [distributing](https://docs.velopack.io/distributing/overview),
  [deltas](https://docs.velopack.io/packaging/deltas),
  [signing](https://docs.velopack.io/packaging/signing),
  [runtimes](https://docs.velopack.io/packaging/runtime),
  [GitHub Actions](https://docs.velopack.io/distributing/github-actions),
  [update sources](https://docs.velopack.io/integrating/update-sources),
  [deploy CLI](https://docs.velopack.io/distributing/deploy-cli),
  [IVelopackLocator](https://docs.velopack.io/reference/cs/Velopack/Locators/IVelopackLocator),
  [FAQ](https://docs.velopack.io/troubleshooting/faq)
- [JetBrains Toolbox App FAQ](https://www.jetbrains.com/help/toolbox-app/frequently-asked-questions.html), for how the two tier arrangement behaves in practice
- [Velopack issue 128, Linux update failures](https://github.com/velopack/velopack/issues/128)
- [AppImage leaks bundled library environment into child processes, breaking system git and curl and python3](https://github.com/block/buzz/issues/2315)
- [AppImage leaks LD_LIBRARY_PATH to spawned git, breaking HTTPS](https://github.com/github/copilot-cli/issues/3925)
- [AppImageKit issue 616, restoring environment variables for processes outside the AppImage](https://github.com/AppImage/AppImageKit/issues/616)
- [AppImage desktop integration](https://docs.appimage.org/reference/desktop-integration.html) and [AppImageLauncher](https://github.com/TheAssassin/AppImageLauncher)
- [Flatpak sandbox permissions](https://docs.flatpak.org/en/latest/sandbox-permissions.html) and [flatpak-spawn](https://man7.org/linux/man-pages/man1/flatpak-spawn.1.html)
- [Snap and Flatpak and AppImage compared](https://computingforgeeks.com/snap-vs-flatpak-vs-appimage/)
- [ZipArchiveEntry.ExternalAttributes](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.externalattributes), [best practices for ZIP and TAR archives](https://learn.microsoft.com/en-us/dotnet/standard/io/zip-tar-best-practices) and [dotnet/runtime issue 1548, ZipFile support for unix permissions](https://github.com/dotnet/runtime/issues/1548)
- [dotnet/coreclr pull 5030, named mutexes for cross process synchronisation](https://github.com/dotnet/coreclr/pull/5030), which is where the Linux behaviour and the `Global\` requirement are described
- [XDG activation protocol](https://wayland.app/protocols/xdg-activation-v1), [on window activation](https://blog.broulik.de/2025/08/on-window-activation/) and [Electron using XDG_ACTIVATION_TOKEN for its single instance lock](https://github.com/electron/electron/pull/43481)
- [dotnet/sdk issue 18282, self contained publish with multiple executables](https://github.com/dotnet/sdk/issues/18282)
- [dotnet/runtime issue 53834, deploying multiple executables as one self contained set](https://github.com/dotnet/runtime/issues/53834)
- [Trusted Signing pricing](https://azure.microsoft.com/en-in/pricing/details/trusted-signing/) and [Trusted Signing for individual developers](https://techcommunity.microsoft.com/blog/microsoft-security-blog/trusted-signing-is-now-open-for-individual-developers-to-sign-up-in-public-previ/4273554)
- [NetSparkle](https://github.com/NetSparkleUpdater/NetSparkle) and [Onova issue 22, restart with Avalonia](https://github.com/Tyrrrz/Onova/issues/22)
- [Avalonia deployment and Native AOT](https://docs.avaloniaui.net/docs/deployment/native-aot)
