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

| | Windows | Linux |
|---|---|---|
| Output | `Setup.exe` | one `.AppImage`, no installer |
| Install location | `%LocalAppData%\Kitbash`, no elevation | wherever the person put the file |
| Feed | `releases.win.json` over static HTTP | `releases.linux.json`, same |
| Update | swaps the whole `current` folder | replaces the AppImage in one rename |
| Deltas | zstd patches per file | yes, after the first update |

**`VelopackApp.Build().Run()` has to be the first statement in `Main`.** Velopack reruns
the binary with `--veloapp-` hook arguments during install, update and uninstall, and those
hooks exit from inside `Run`. It is wrapped, and a launch carrying a hook argument returns
a code rather than opening a window, since a window in the middle of an install is worse
than stopping.

**`vpk` cross compiles.** The OS directive goes before the verb, so `vpk [win] pack` runs
on Linux and `vpk [linux] pack` runs on Windows. One runner builds both releases, which is
what `build/release.sh` does. Only macOS needs a Mac. Signing a Windows package still needs
`signtool.exe`, and nothing is signed.

## Nothing a person owns may sit under the install root

**Velopack's uninstaller deletes `%LocalAppData%\{packId}` whole.** The pack id is
`Kitbash`, so that folder is the install root, and it holds the application alone.
`WindowsUserDirectories` puts state at `%LOCALAPPDATA%\KitbashData\State` and cache at
`%LOCALAPPDATA%\KitbashData\Cache` for exactly this reason. Linux never had the problem.

**This applies to anything added later**, so a new user directory goes beside the install
root and never inside it. The workspace list, every setting, every installed engine and
every installed tool are what the rule is protecting.

`--mainExe` is required rather than optional, since it otherwise defaults to the pack id
and the executable is named after the project.

**A launcher update must not reach into a running tool.** Velopack replaces the whole
`current` directory on Windows and will offer to close whatever holds a file in it. Tools
live under the data directory, so a launcher update replaces the launcher and open tools
carry on.

## Every path through startup ends with a window

The rule the update path is built to. A copy that fails to update and shows nothing is
worse than one that never tried.

- **`CheckAsync` never throws**, cancellation included.
- **A failed download closes the dialog and returns.** Measured with a package whose
  SHA256 was made not to match: the launcher opened 1.9 seconds in and the AppImage on
  disk was untouched.
- **Any failure in the update path is caught in `App.StartAsync`**, which opens the
  launcher afterwards whatever happened.

### The check has a three second deadline

Nothing is drawn until the update is settled, so the check is a blank screen and is
bounded.

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

**The dialog is the only window while it runs.** It opens with `Show` rather than
`ShowDialog`, centres on the screen and sits in the task bar. `ShutdownMode` is
`OnExplicitShutdown` until the launcher is up, since the dialog closing would otherwise be
a last window closing and end the app before anything opened.

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

This machine is Linux. The Windows package is built here, since `vpk` cross compiles, and
none of it has been executed. Still unwatched: that `Setup.exe` installs without elevation,
what the uninstaller actually removes now that state and cache have moved out of the install
root, whether an automatic update triggers SmartScreen, and everything about the per machine
MSI.
