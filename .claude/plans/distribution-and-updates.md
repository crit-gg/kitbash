# Distribution and updates

How Workbench and its tools reach a machine, and how they stay current, on Windows
and on any Linux distribution.

Nothing here is built. This is the research and the recommendation, written before
the first tool exists, because the answer changes how the projects are published and
that is cheaper to decide now.

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
| Install location | `%LocalAppData%\Workbench`, no elevation | wherever the person put the file |
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

**Velopack does not cross compile.** A Windows package is built on Windows and a Linux
package on Linux. That is two CI jobs, and it matches how the app has to be tested anyway.

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

This is the one that would ship broken and stay broken for a while.

An AppImage mounts itself and sets `LD_LIBRARY_PATH` and friends at the mount point so
its own binaries find their bundled libraries. Those variables are inherited by every
child process, including programs on the host that have nothing to do with the bundle.
The host program then loads our older libraries instead of its own and fails on a missing
symbol. This is a known AppImage design problem with a long trail of reports against
other applications. Two recent ones are worth reading, because both are this exact case:
GitHub's Copilot CLI AppImage broke `git` over HTTPS through a bundled `libnghttp2`, and
Buzz broke system `git`, `curl` and `python3` the same way.

**Workbench runs `git` on a beat and hands folders and URLs to the desktop opener.** So
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

**So Workbench integrates itself on first run.** Write a `.desktop` file into
`$XDG_DATA_HOME/applications` with `Exec` pointing at `$APPIMAGE`, and the icon into the
hicolor theme under the same root. Both paths already come from `IUserDirectories`, and
the file is a freedesktop specification we can read rather than a guess. Remove both when
the app is uninstalled.

This is ours to write. It is small, and it is the difference between a launcher and a
file in Downloads.

### 4. The update fails across filesystems

Velopack downloads to `/var/tmp` and renames the result over the existing AppImage. A
rename cannot cross a filesystem. Anyone whose home is a separate mount, or who keeps the
file on a NAS or an external disk, gets a failed update. This is reported and the working
answer so far was moving the file, which is a workaround rather than a fix.

Test it before release. If it still fails, the answer is to stage the download beside the
target rather than in `/var/tmp`.

One more thing worth knowing rather than fixing: **the first update on Linux is always a
full download.** Only the Windows installer ships a complete package, so there is nothing
for the first delta to diff against.

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

If this ever ships to people outside the team, revisit it. Azure Trusted Signing is about
ten dollars a month and carries SmartScreen reputation immediately, and Velopack drives it
through `--signTemplate`, so turning it on later is a build argument rather than a rewrite.

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

**NetSparkle.** Genuinely cross platform, with a prebuilt Avalonia UI and Ed25519
signatures, which is more than Velopack offers on the presentation side. It stops at
downloading and launching an installer. There is no installer on Linux, so the apply and
restart half is ours to write, which is the hard half.

**Onova.** Automatic restart does not work with Avalonia applications.

**Squirrel.Windows and Clowd.Squirrel.** Windows only, and Velopack supersedes both.
There is a migration guide from either.

**Writing our own.** Checking a version file and downloading a zip is a day's work. Doing
an atomic swap of a running application on two platforms, with a rollback when it fails
part way, is not, and getting it wrong leaves people with a broken install and no way to
repair it from inside the app.

## The tool system

The part that is ours to write. It is small, and the reason it is small is worth saying
plainly: **the hard part of an updater is replacing a running program with itself, and
that never happens here.** The launcher installs a tool, and the launcher is not the tool.
Velopack earns its place on the launcher for exactly the problem that is absent below.

### Where a tool lives

```
<data directory>/tools/<id>/<version>/
```

Which is `$XDG_DATA_HOME/workbench/tools/` on Linux and `%LocalAppData%\Workbench\tools\`
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
not in the contract. A .NET tool takes `Workbench.Core` and `Workbench.Ui` and gets all of
that for free, which is why they exist. Anything else reads a path from its arguments and
does whatever it likes. **The launcher never needs to know which.**

`Workbench.Core` is therefore a library that makes writing a .NET tool easy, rather than
the contract every tool obeys. That is a smaller claim than it currently makes, and a
truer one.

`ITool`, `IToolActivation` and `IToolRegistry` stay useful as the launcher's own model of
an installed tool. Nothing in the launcher may assume a tool is managed code.

**This contradicts what `.claude/CLAUDE.md` currently says**, in two places: that
`Workbench.Core` is the contract shared by every tool, and that tools are registered
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

## Hosting: our own server, unlisted

Decided. Static files on a web server we run, at a URL that is not linked from anywhere.

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
  `Workbench.Core` is a library for writing .NET tools, not the contract.
- **Semantic versions everywhere**, launcher and tools alike.
- **One launcher at a time**, in a release build only.
- **Our own server, unlisted, over HTTPS.** No GitHub Releases, no Velopack Flow.
- **Nothing is signed.** One SmartScreen warning at first install, on purpose.

## Open questions

Do not build on an assumption for any of these.

- **Can a tool be started without the launcher?** Workspace discovery currently assumes a
  tool can be launched from a subdirectory on its own. If that stays true, each installed
  tool needs its own shortcut or desktop entry, written by the launcher at install and
  removed at uninstall, pointing into a versioned directory that moves on every update.
  That is a real piece of work and it is easy to miss when planning this.
- **What happens to a tool's settings when it is uninstalled?** Workspace settings under
  `.workbench/` belong to the workspace and should survive. Application state for that
  tool probably should not. Decide before the first uninstall exists.
- **Does the launcher offer a tool it has no payload for?** A tool with a Linux build and
  no Windows one will exist. Showing it and refusing it is friendlier than hiding it, and
  either way it is a decision rather than an accident.
- **Per user or per machine on Windows.** Per user is the default, needs no administrator,
  and is almost certainly right. Per machine needs the MSI and an administrator.
- **One channel or two.** A preview channel is nearly free in Velopack and it is the only
  way to test the update path other than in production.

## The smallest thing that proves it

In order. The first four are the launcher tier and can be done now, before any tool exists.

1. Publish the launcher self contained and `vpk pack` it on Linux. Run the AppImage.
2. **Run `git` from inside the AppImage and see whether it breaks.** If it works, find out
   why before believing it, because the answer depends on which libraries got bundled and
   it can change with any Avalonia update.
3. Release a second version to a local directory feed and update to it. Then do it again
   with the AppImage on a different filesystem from `/var/tmp`.
4. Write the `.desktop` integration and confirm the launcher appears in the menu on two
   different desktops.
5. **Put a shell script in `$XDG_DATA_HOME/workbench/tools/hello/1.0.0/` with a manifest
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

This machine is Linux. Everything about `Setup.exe`, SmartScreen and the per machine MSI is
read from documentation and has not been run, and the claim that automatic updates escape
SmartScreen is reasoning from how the mark of the web works rather than something anyone
has watched happen. The Linux claims are also documentation so far, and the steps above are
exactly the ones this machine can settle.

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
