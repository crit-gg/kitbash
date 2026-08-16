---
name: kitbash-updates
description: "Kitbash distribution and self updating. Velopack, the pack id and the install root, the startup update path and its deadline, the release workflow and the feed, code signing and the AppImage. Read before touching Program.cs, the update path, build/release.sh or the release workflow."
---

## The two tiers

**Velopack installs and updates the launcher. The launcher installs and updates tools.**
This page is the launcher tier alone. The tool tier is `.claude/plans/tool-distribution.md`
and it shares none of this machinery.

**A tool never checks for its own update.** The launcher does it, for itself and for every
tool, so a tool stays a tool.

**A tool is a standalone program in any language.** The launcher discovers it, installs it,
starts it, and hosts nothing. `Kitbash.Core` is a library that makes writing a .NET tool
easy, not the contract every tool obeys.

## Velopack

Velopack 1.2.0, MIT, the successor to Squirrel.Windows. Only the launcher takes it, since
neither library needs it.

| | Windows | Linux | macOS |
|---|---|---|---|
| Output | `Setup.exe` | one `.AppImage`, no installer | a `.app` in a `.zip`, plus a `.pkg`. Never a `.dmg` |
| Install location | `%LocalAppData%\Kitbash`, no elevation | wherever the person put the file | wherever the person put it, `/Applications` from the pkg |
| Feed | `releases.win.json` over static HTTP | `releases.linux.json`, same | `releases.osx.json`, same |
| Update | swaps the whole `current` folder | replaces the AppImage in one rename | renames the whole bundle aside and deletes it |
| Deltas | zstd patches per file | yes, after the first update | yes, after the first update |

**The macOS column is read from the Velopack 1.2.0 source rather than run.** Nothing has been
packed for macOS yet.

**`VelopackApp.Build().Run()` has to be the first statement in `Main`.** Velopack reruns
the binary with `--veloapp-` hook arguments during install, update and uninstall, and those
hooks exit from inside `Run`. It is wrapped, and a launch carrying a hook argument returns
a code rather than opening a window, since a window in the middle of an install is worse
than stopping.

**`vpk` cross compiles between Windows and Linux.** The OS directive goes before the verb, so
`vpk [win] pack` runs on Linux and `vpk [linux] pack` runs on Windows. One runner builds both,
which is what `build/release.sh` does. Signing a Windows package still needs `signtool.exe`,
and nothing is signed.

**macOS is harder than needing a Mac: `vpk [osx] pack` is not registered as a command at all
unless `vpk` is running on one.** In its own `Program.cs` the pack command is added inside an
`if (VelopackRuntimeInfo.IsOSX)`, so on Linux it is an unrecognised command rather than a
capability that fails. `[osx] bundle` is registered everywhere but cannot produce a release.
So the release pipeline needs a second job on a macOS runner. `build/release.sh` should test
`uname` and say so, since the failure otherwise reads as a typo.

## Nothing a person owns may sit under the install root

**Velopack's uninstaller deletes `%LocalAppData%\{packId}` whole.** The pack id is
`Kitbash`, so that folder is the install root, and it holds the application alone.
`WindowsUserDirectories` puts state at `%LOCALAPPDATA%\KitbashData\State` and cache at
`%LOCALAPPDATA%\KitbashData\Cache` for exactly this reason. Linux never had the problem.

**This applies to anything added later**, so a new user directory goes beside the install
root and never inside it. The workspace list, every setting, every installed engine and
every installed tool are what the rule is protecting.

**On macOS the rule is stronger, because it bites on every release rather than on uninstall.**
An update renames the whole old `.app` aside and deletes it, so anything written inside the
bundle is gone at the next version, not at the end. `/Applications` is usually not writable by
the person running the app either, so the writes would fail first. `MacUserDirectories` puts
everything under `~/Library`, and the `kitbash-settings` skill has the paths.

`--mainExe` is required rather than optional, since it otherwise defaults to the pack id
and the executable is named after the project.

**A launcher update must not reach into a running tool.** Velopack replaces the whole
`current` directory on Windows and will offer to close whatever holds a file in it. Tools
live under the data directory, so a launcher update replaces the launcher and open tools
carry on.

## The splash is the update's surface

**There is no update dialog.** `ui:SplashWindow` is the only window the launcher has until
it opens, and the update reports into it. `.claude/plans/splash-window.md` has the window
itself and the order a host shows one in.

`App.StartAsync` is the whole of startup: build the splash, show it, ask the feed, fetch what
it offers, then load the look and open the launcher. **The splash is up before the feed is
asked**, so the check is a wait a person can see rather than a blank screen.

**The wording lives in `UpdateStages`, not in the window.** It turns a whole percent into the
status line, the fraction and the readout, so both of its rules can be tested without drawing
anything: the size is the feed's stated one humanized once, and a hundred percent says
Checking the download because Velopack runs the checksum inside the same call.

**A full bar is stated rather than waited for, then held for 600ms.** Velopack's last report
is not reliably a hundred, so `Downloading(100)` is called once the fetch returns, and the
fill takes 180ms to travel, so moving on any sooner would mean a hundred percent was never
seen.

**The swap itself is indeterminate.** After the dwell the bar goes back to sweeping under
"Restarting Kitbash", since replacing the copy on the machine reports nothing and takes what
it takes.

**A feed offering nothing takes the progress row back down**, so the card collapses to the
mark and the name while the launcher is built. A failed download lands there too.

**The splash is up for at least two seconds** before the launcher replaces it. Only the path
that opens the launcher waits the floor out, since a download passes it many times over.

## Every path through startup ends with a window

The rule the update path is built to. A copy that fails to update and shows nothing is
worse than one that never tried. **The one exception is a person dismissing the splash**,
which is a request to leave rather than a failure.

- **`CheckAsync` never throws**, cancellation included.
- **A failed download reports nothing further and returns**, and the launcher opens on the
  copy already installed. Measured before the splash took this over, with a package whose
  SHA256 was made not to match: the launcher opened 1.9 seconds in and the AppImage on
  disk was untouched.
- **Any failure in the update path is caught in `App.StartAsync`**, which opens the
  launcher afterwards whatever happened.

**Dismissing the splash during a download quits Kitbash.** It cancels the fetch, and
`SplashWindow` ends the app itself because no other window is up. This departs from what the
dialog did, which was to cancel and open the launcher anyway. `StartAsync` checks for it after
every await, so nothing opens a window after the app has been told to go.

### The check has a three second deadline

The splash is drawn while the feed is asked, so this bounds a visible wait rather than a
blank screen. The number has not moved since that changed.

**The check says nothing for its first second.** A feed that answers sooner never draws the
progress row at all, since a row going up and straight back down is a flash on every ordinary
launch. `App.SayIfSlow` is the rule: `Task.WhenAny` the check against the patience, and report
only if the check is still running. **Once the row is up it is held for 600ms**, measured from
the report, so a check that answered at 1.1 seconds does not blink and a check that ran to the
deadline waits no further. The two second splash floor usually swallows the hold entirely.

**A cancellation token does not bound it.** `UpdateManager.CheckForUpdatesAsync` takes no
token, so one handed to `Task.Run` only stops it starting. Measured before this was
understood: a server that accepted the connection and never replied held the window back
for over thirty seconds. `CheckAsync` stops waiting rather than cancelling, with
`Task.WhenAny` against the deadline, and reads the abandoned task's failure so it is never
unobserved.

**`SimpleWebSource.Timeout` is a second backstop at fifteen seconds.** Velopack's default
is thirty minutes. The feed is read with `GetStringAsync`, so the timeout bounds it end to
end, while a package is read with `HttpCompletionOption.ResponseHeadersRead`, so for a
download it bounds only the wait for the first response.

Measured, launch to window, against a baseline start of about 1.7 seconds: a server that
accepts and never replies 4.8s, nothing listening 1.8s, a host that does not resolve 1.7s,
a folder that is not there 1.6s.

**`ShutdownMode` is `OnExplicitShutdown` until the launcher is up**, since the splash closing
would otherwise be a last window closing and end the app before anything opened. `Open` puts
it back. The splash is also `desktop.MainWindow` until the launcher replaces it, so a second
copy asking this one to come forward has something to raise.

## The feed

**`updates.feed` is a real key on no page.** Where releases come from is the app's answer
rather than a person's, so `UpdateSettingsSchema.DefaultFeed` is compiled in and the
Updates page draws the running version alone. It is an ordinary setting otherwise, read
from its descriptor out of the application settings file, so a copy can be pointed
somewhere by hand:

```toml
[updates]
feed = "https://..."   # or an absolute path to a folder
```

`ISettingsWriter` refuses the key, since it guards against writing anything a page did not
declare. Nothing announces it either, which is why it is written down here.

### Four things the host has to get right

**HTTPS with a certificate that validates.** Nothing is signed, so the feed is the entire
chain of trust. Velopack checks the size and hash of every package against the feed and
does nothing at all about a tampered feed. Over plain HTTP anyone who can answer for that
hostname can hand every installation a program of their choosing.

**Do not cache the feed. Do cache the packages, and only the packages.** A long cache
lifetime on `releases.*.json` hides new releases for as long as it lasts, and the symptom is
that updates quietly stop happening for some people. **Only a `.nupkg` carries its version
in its name**, so only those never change content. The installers, `Kitbash.AppImage`,
`Kitbash-win-Setup.exe` and the portable zip, are overwritten in place on every release and
must not be cached long, since the download links point at those names.

**Directory listing off**, and **serve bytes unchanged**. A proxy that recompresses breaks
the hash check, and the failure reads as a corrupt download rather than a bad server.

**The base URL is compiled into the application, so treat that directory as public.**
Unlisted keeps it off a search engine and is not access control. The bucket has to stay
public whatever else changes, because `SimpleWebSource` appends its own query string and
can neither sign a URL nor add a header.

**Each product takes its own prefix under the base**, so the launcher feed is
`/kitbash/` and a tool published beside it takes its own name.

### The pipeline

`.github/workflows/release.yml` publishes on a push to main. `_version.yml` works the
version out from tags and the commits since the last one, so no version lives in a tracked
file, and `[major]`, `[minor]` or `[version:x.y.z]` in a commit message moves it further
than the patch it would take on its own. `build/release.sh` publishes both runtimes and
packs both.

**A push that touches nothing the packed app is built from does not release.** The trigger's
`paths-ignore` holds everything outside `src/Kitbash`, `src/Kitbash.Ui`, `src/Kitbash.Core`,
`icons`, `build` and `Directory.Build.props`, so the gallery, the tests and the icon scripts
release nothing. `workflow_dispatch` carries no filter and is how a skipped push is released.

**`vpk download s3` runs before the pack and `vpk upload s3` after it.** The download is
what makes a delta possible, since `vpk pack` diffs against the previous full package
sitting in the feed directory. B2 serves an S3 compatible API, which is why the bucket beat
a plain web server: there is no `vpk upload http`.

**One release at a time**, through a concurrency group, since two would work out the same
version and race on the feed. The tag is written only after a release really published.

**`VPK_VERSION` is matched to the Velopack package reference.** A newer `vpk` can write a
feed this launcher's Velopack cannot read.

**Velopack takes three part semantic versions only.** `1.0.0`, `1.0.0-preview.1` and
`2026.8.1` are fine and `1.0.0.0` is refused. **Semantic versions everywhere**, launcher
and tools alike, so a comparison is the same comparison wherever it happens. The tool
manifest's own version is the one exception and it is a file format number rather than a
product version.

## One launcher at a time

**Built.** `ISingleInstance` in Core, taken by the launcher in
`App.OnFrameworkInitializationCompleted` before the update check and before any window. A
later copy asks the first to come forward and exits without drawing.

The launcher is the only writer of things that have no merge, and two launchers means the
second can undo the first. Two of the three original reasons have since softened, and they
are recorded because they decide how much this guard is load bearing:

- **Application state** used to be rewritten from the model on every write. It goes through
  `TomlDocument` now, one edit per key against a file that is read again first, so two
  launchers writing different keys both land. What is left is arrays read whole and written
  back, `workspaces.known` and the imported engine list.
- **The launcher update** is already guarded by Velopack's own `.velopack_lock`.
- **The tool directory** is guarded where it happens: the staging directory and the part
  file carry the process id, so two installs of one tool cannot share either.

### A file lock, not a named mutex

The mutex prefix means opposite things on the two platforms. Linux needs `Global\` for a
named mutex to be visible across processes at all, and on Windows `Global\` is machine wide
and would stop a second person signed in from opening the launcher. `FileShare.None` is
exclusive on both, so it is one piece of code, and **the kernel drops the handle when the
process dies**, which a pid file does not: a stale one locks somebody out of their own
launcher.

The lock is `instance.lock` under a fourth kind of user directory, `IUserDirectories.RuntimeFor`.
`$XDG_RUNTIME_DIR` on Linux, which **can be unset** over ssh or a login that does not go
through systemd, so it falls back to the cache directory. Local application data on Windows,
**never the configuration directory**, which roams and would take a lock to another machine.

### The handoff

A named pipe, which is a unix domain socket underneath on Linux. **The name carries the user
name**, because a pipe is machine wide on Windows and a socket in the shared temp directory on
Linux, so two people signed in would otherwise collide. Another local user can therefore
connect to it, and all they can do is ask the window to come forward.

**A copy that cannot hand over opens instead of exiting.** The first copy takes the lock
before it starts listening, so a copy started in that gap waits two seconds and then carries
on. Two launchers is a survivable state and an app that will not open is not.

**Coming forward is `Window.Activate`, and X11 rules apply.** `UsePlatformDetect` selects X11
on Linux, so under a Wayland session the app is an XWayland client and the compositor treats
the raise as an X11 one. **The original design carried an `XDG_ACTIVATION_TOKEN` from the
second copy and nothing does, because a native Wayland client is the only thing that needs
one.** If `Avalonia.Wayland` is ever adopted, the token has to be captured by the second copy
at start, since it cannot be recovered later, and the wire format grows a field then.

### Several copies on purpose

**`KITBASH_MANY_LAUNCHERS` set to anything turns the guard off.** This departs from the
original design, which was `#if !DEBUG`: a compile time exemption means the behaviour a
person sees can never be exercised in a debug build, and this is a behaviour worth watching.
So the guard runs in every build and a developer who wants two side by side sets the
variable.

Measured on this machine, across real processes: three later copies each handing over and
exiting while the first was told each time, a first copy that exited normally leaving the
lock free for the next, **a first copy killed with SIGKILL leaving it free as well**, the
override letting a second copy through, and an unset `XDG_RUNTIME_DIR` putting the lock in
the cache directory. Through two real launchers headless: the second drew no window and the
first raised its own. **Whether a compositor honours the raise is the compositor's business
and was not measured here.**

## Two decisions that stand, with what they cost

### Nothing is signed

On Linux it costs nothing, since no desktop verifies an AppImage signature. On Windows it
costs one SmartScreen warning at first install, and Defender may quarantine outright.

**Reputation was never going to accrue anyway.** SmartScreen builds reputation per
certificate for a signed application and per file for an unsigned one, and every release is
a new file. So the choice is one warning forever against paying, not paying against
waiting.

**Updates should not hit SmartScreen at all**, because the warning comes from the mark of
the web that a browser writes, and Velopack fetches packages itself. **That is expected
behaviour rather than something watched, so confirm it on the first Windows test.** A
warning on every automatic update is bad enough to reopen this.

Azure Artifact Signing is about ten dollars a month, carries reputation immediately, and
Velopack drives it through `--signTemplate`, so turning it on later is a build argument.

**On macOS the same decision costs more, and three parts of it are separate.**

**Ad hoc signing is not optional on Apple silicon**, since the kernel refuses to execute a
Mach-O with no signature at all. The SDK ad hoc signs the apphost and Microsoft signs the
runtime pack, so a self contained publish runs. Verified here: `codesign -dv` on an osx-arm64
publish reports `Signature=adhoc` and `codesign --verify` passes.

**An applied update should carry no quarantine.** `com.apple.quarantine` is written by the
downloading process, which opts in through `LSFileQuarantineEnabled` or by using a framework
that does. Velopack fetches over `HttpClient` from inside our process and applies through a
helper doing ordinary writes, so neither opts in. This is the same reasoning already applied
to SmartScreen and it should be confirmed the same way, by taking a real update and running
`xattr -l` on the bundle.

**The first install is the part that hurts, and the usual advice is out of date.** A browser
does write the attribute, so the downloaded artefact is refused. **Control clicking and
choosing Open no longer works: Apple removed that bypass in macOS 15.** The flow is now to be
refused, then go to System Settings, Privacy and Security, and press Open Anyway. The one line
alternative is `xattr -d com.apple.quarantine`.

**Point people at the zip rather than the pkg.** An unsigned `.pkg` asks for an admin password
to install something macOS is simultaneously refusing to verify, which reads much worse than
dragging a `.app` to Applications. Build both, since the pkg costs nothing.

An Apple Developer Program membership plus notarisation removes all of it, and Velopack drives
that through `--signAppIdentity`, `--signEntitlements` and `--notaryProfile`, so it is three
arguments and a keychain profile, the same shape as the Windows case.

### AppImage on Linux, kept without enthusiasm

**Velopack builds nothing else on Linux**, so dropping the format means dropping Velopack
there and splitting the launcher tier in two. Nothing surveyed covers both halves: NetSparkle,
Onova, Sewer56.Update and Qt Installer Framework each stop short, and deb and rpm support
is Velopack issue 370, open since November 2024.

The honest replacement is ours to write and it is the tool installer's own machinery: a
tar.gz into `$XDG_DATA_HOME/kitbash/app/<version>/`, a `current` symlink, extract to
`<version>.incoming`, rename, then rename a new symlink over `current`, which is atomic on
POSIX.

**What should reopen this:** the tool installer landing, which it now has, so the versioned
directory and the rename already exist and could be used here too. The environment leak
growing, if a Velopack release starts exporting library paths beyond `PATH`. Anyone
reporting the AppImage as awkward. Or a second Linux format appearing in Velopack.

`IBundleEnvironment` is the only code that exists purely because of the format, and the
`kitbash-platform` skill has it. **The first update on Linux is always a full download**,
since only the Windows installer ships a complete package, so the first delta has nothing to
diff against.

## What could not be tested here

**Development is on an Apple silicon Mac now, so Windows and Linux are both unwatched.**
The Windows package is built by CI, since `vpk` cross compiles, and none of it has been
executed. Still unwatched there: that `Setup.exe` installs without elevation, what the
uninstaller actually removes now that state and cache have moved out of the install root,
whether an automatic update triggers SmartScreen, and everything about the per machine MSI.

**Nothing has been packed for macOS at all.** `build/release.sh` has no `osx` branch yet, the
repository holds no `.icns`, and the release workflow has one job on Linux. The launcher does
run from an osx-arm64 publish, ad hoc signed, and Velopack correctly reports it is not in a
bundle and declines to update it. Everything else in the macOS column above is read from the
Velopack source rather than measured.
