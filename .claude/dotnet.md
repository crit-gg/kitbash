# .NET, found and installed

Research only. Nothing here is built. This is what a `Kitbash.Core/DotNet` would have
to know, written the way `avalonia.md` is: what was measured, where the traps are, and
what the answer should be when someone builds it.

Measured on 1 August 2026, on Garuda Linux, x64, with .NET SDK 10.0.302 from the Arch
package `dotnet-host-bin`. Everything Windows in here comes from Microsoft's own
documentation and could not be run, which is called out where it matters.

Two things are wanted. A way to install or update .NET for a person on any platform, and
an integration that runs `dotnet` the way `Kitbash.Core/Git` runs git. They are the same
piece of work, because the thing that installs has to find what is already there first,
and finding is most of what the integration does.

## Before anything, what needs it

Kitbash is a .NET app. If it ships framework dependent it cannot be the thing that
installs the runtime it needs in order to start, so the launcher publishes self contained
or this feature is unreachable on a machine with no .NET. `Directory.Build.props` already
names both runtime identifiers and the publish commands in `CLAUDE.md` already pass
`--self-contained`. Keep it that way and the launcher runs on a machine that has never
seen .NET.

So the SDK being installed is never the one Kitbash runs on. It is the one a **workspace**
needs, to build the Godot project's C# assembly. That changes the question from "is .NET
up to date" to "does this workspace build", and those have different answers. See
What version, below.

## Where the truth about versions lives

Microsoft publishes release metadata as JSON, and it is the only supported way to ask what
exists without scraping a download page.

| File | Size | What it answers |
|---|---|---|
| `https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json` | 7 KB | every channel, its latest release, its support phase, its end of life date |
| `.../release-metadata/{channel}/releases.json` | 935 KB for 10.0 | every release in one channel, every file, every URL, every hash |

**Ask the index, not the channel.** The index answers "is there something newer" in 7 KB.
The 10.0 channel file is 935 KB and is only needed once a download URL and its hash are
actually wanted. Measured, both.

The index entry for a channel:

```json
{
  "channel-version": "10.0",
  "latest-release": "10.0.10",
  "latest-release-date": "2026-07-14",
  "security": true,
  "latest-runtime": "10.0.10",
  "latest-sdk": "10.0.302",
  "support-phase": "active",
  "release-type": "lts",
  "eol-date": "2028-11-14"
}
```

`support-phase` is one of `preview`, `active`, `maintenance`, `eol`. `security` says the
latest release carried CVE fixes. Those two together are the whole "should this person
update" question, and neither needs the channel file.

As measured on the day: 11.0 is `preview` and STS, 10.0 is `active` and LTS, 9.0 and 8.0
are both `maintenance` and both end on 10 November 2026, 7.0 and 6.0 are `eol`.

A file inside a channel's `releases.json` carries its own hash:

```json
{
  "name": "dotnet-sdk-linux-x64.tar.gz",
  "rid": "linux-x64",
  "url": "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.302/dotnet-sdk-10.0.302-linux-x64.tar.gz",
  "hash": "10069bec8783..."
}
```

The hash is SHA-512, hex, lower case. That is what makes downloading it ourselves
defensible, and it is the one thing the official script does not do.

**The hostnames changed and the old ones are gone.** `dotnetcli.azureedge.net`,
`dotnetbuilds.azureedge.net` and `dotnetcli.blob.core.windows.net` were retired in early
2025 after Edgio went under. Everything now comes from `builds.dotnet.microsoft.com`.
Anything found online older than that names a host that no longer resolves.

Both metadata files carry a `signature` block naming a detached PKCS#7 file beside them,
which was fetched and is real:

```json
"signature": { "expiration": "2026-10-12T18:09:51+00:00",
               "file": "releases-index.json.20260714180951.p7s" }
```

Verifying it needs the Microsoft signing chain and gains little over HTTPS to a Microsoft
host plus a per file SHA-512 from inside that same document. Worth knowing it exists.
Not worth building first.

### The aka.ms aliases, and the trap in them

`https://aka.ms/dotnet/{channel}/dotnet-sdk-{rid}.{ext}` redirects to a concrete build.
Measured:

```
aka.ms/dotnet/LTS/dotnet-sdk-win-x64.exe      -> Sdk/10.0.302/dotnet-sdk-10.0.302-win-x64.exe
aka.ms/dotnet/STS/dotnet-sdk-linux-x64.tar.gz -> Sdk/9.0.316/dotnet-sdk-9.0.316-linux-x64.tar.gz
aka.ms/dotnet/10.0/dotnet-sdk-linux-x64.tar.gz-> Sdk/10.0.302/dotnet-sdk-10.0.302-linux-x64.tar.gz
```

**An alias that does not exist returns 200, not 404.** `aka.ms/dotnet/11.0/...`, which is a
real preview channel with no GA alias, redirects to `bing.com` and answers 200 with an HTML
search page. Anything that downloads an alias blind writes a web page into a file named
`.tar.gz` and only finds out when the extract fails. Measured.

So the aliases are useful for a link in a message and useless as a download path. Resolve
the version from the metadata and download the explicit URL, which carries a hash.

## The install surfaces

### Windows

Four ways, all documented, none run here.

**winget**, one package per major version. `Microsoft.DotNet.SDK.10`,
`Microsoft.DotNet.Runtime.10`, `Microsoft.DotNet.AspNetCore.10`,
`Microsoft.DotNet.DesktopRuntime.10`, and the same for 9 and 8. Install is machine wide
and needs elevation. `winget install --id Microsoft.DotNet.SDK.10 --silent
--accept-package-agreements --accept-source-agreements`.

**The standalone installer**, a Burn bundle. `/install /quiet /norestart`, and `/uninstall`
or `/repair` in place of `/install`. Exit 0 is success, 3010 means a restart is wanted,
anything else is an error.

**`dotnet-install.ps1`**, per user, no admin, into `%LocalAppData%\Microsoft\dotnet`.

**Visual Studio**, which keeps its own copy and is not something to reach into.

Two Windows details worth carrying:

- **Updating can remove what a workspace pinned.** The installer's default is
  `RemovePreviousVersion = always`, in `HKLM\SOFTWARE\Microsoft\.NET`. So winget or the exe
  updating 10.0.302 to a later patch takes 10.0.302 away, and a `global.json` with
  `rollForward: disable` on that exact version stops resolving. `never` and `nextSession`
  are the other two values. Do not write that key. Do warn before an update that a pinned
  workspace could lose its SDK.
- **x86 first on PATH is the classic failure.** With both architectures installed,
  `where.exe dotnet` finding `Program Files (x86)\dotnet\dotnet.exe` first produces
  "No .NET SDK was found" while an SDK is plainly installed. On Arm64 Windows the x64 SDK
  lands in `C:\Program Files\dotnet\x64\`, which is a second copy under the first one's roof.

### Linux, and why a distro matrix loses

Microsoft's package feed no longer covers the interesting distributions. As of .NET 9,
`packages.microsoft.com` publishes only for Azure Linux, Debian, openSUSE Leap and SLES.
Alpine, CentOS Stream, Fedora, RHEL and Ubuntu publish their own. Arch publishes its own
and is on nobody's list.

Ubuntu alone needs a table to describe, and the table moves every six months:

| Ubuntu | Built in feed | Backports PPA | Microsoft feed |
|---|---|---|---|
| 26.04 | 10.0 | 9.0, 8.0 | none |
| 25.10 | 10.0, 9.0, 8.0 | none | none |
| 24.04 | 10.0, 8.0 | 9.0, 7.0, 6.0 | none |
| 22.04 | 8.0, 7.0, 6.0 | 10.0, 9.0 | 8.0, 7.0, 6.0, 3.1 |

Ubuntu's own SDK builds are always the `.1xx` feature band. A workspace wanting a later
band on 22.04 has to take the Microsoft feed, which is the exact mixing that breaks things.

**Mixing two feeds is the documented failure and it is ugly.** Both `/usr/lib/dotnet` and
`/usr/share/dotnet` end up present and `dotnet` fails with things like "The required
library libhostfxr.so could not be found", "The folder /usr/share/dotnet/host/fxr does not
exist", or a missing `FrameworkList.xml`. Unpicking it means removing every `dotnet*`,
`aspnet*` and `netstandard*` package, then pinning one origin to priority -10 in
`/etc/apt/preferences` or adding `excludepkgs` to a `.repo` file, then reinstalling. That is
a system administration task and it is not something an app should ever attempt on
somebody's machine.

**Microsoft's own tool gave up on the matrix.** The .NET Install Tool for VS Code carries
`distro-support.json`, and it covers three distributions: Debian, Ubuntu and Red Hat
Enterprise Linux. Everything else falls back to a user local install. Its per distro
record is worth reading as prior art, because it names exactly what a matrix has to know
per entry:

```
installCommand, uninstallCommand, updateCommand, searchCommand, isInstalledCommand,
packageLookupCommand, readSymLinkCommand, expectedDistroFeedInstallDirectory,
expectedMicrosoftFeedInstallDirectory, installedSDKVersionsCommand,
installedRuntimeVersionsCommand, currentInstallationVersionCommand,
currentInstallPathCommand, packages[], versions[]
```

Every command in it carries `runUnderSudo`. Ubuntu's install is three commands, starting
with `dpkg --configure -a` to clear a half finished state, and each apt call passes
`-o DPkg::Lock::Timeout=180` because another package manager may hold the lock.

**Keying that matrix on the distribution does not work here.** This machine's
`/etc/os-release` reads:

```
PRETTY_NAME="Garuda Linux"
ID=garuda
ID_LIKE=arch
```

There is no `VERSION_ID` at all, because it is a rolling release. Microsoft's file keys on
the distribution name and a version string, so this machine matches nothing, and the
machine this app is developed on is exactly the case the matrix cannot see. `ID_LIKE=arch`
is the only usable signal and it says a family, not a package set.

The project's second question, what does the other distribution do here, has an answer for
.NET and the answer is that nobody knows and the list is open. So do not write the list.

**Snap exists and has caveats.** `sudo snap install dotnet-sdk-100`, one package per major
version, from Canonical. It creates no unversioned `dotnet`, so it wants a symlink into
`/usr/local/bin` and a `DOTNET_ROOT` of `/snap/dotnet-sdk-100/current/usr/lib/dotnet`.
Microsoft's own documentation says global tools may not work and `dotnet watch` is a known
failure, and recommends the install script instead if either is wanted. Detect it, do not
create it.

### The official install script, and what it does not do

`https://dot.net/v1/dotnet-install.sh` and `.ps1`. 1887 lines of bash in the one that was
read. It downloads a tarball and unpacks it into `$HOME/.dotnet`, or
`%LocalAppData%\Microsoft\dotnet` on Windows, or wherever `--install-dir` or the
`DOTNET_INSTALL_DIR` variable says.

Its version selection is the part worth copying:

- `--channel` takes `LTS`, `STS`, `A.B`, or `A.B.Cxx` for a feature band. Default `LTS`.
- `--quality` takes `daily`, `preview`, `GA`, only alongside `--channel`, and is ignored on
  `LTS` and `STS`.
- `--version` takes a three part build and overrides both.
- `--runtime` takes `dotnet`, `aspnetcore` or `windowsdesktop`, and its absence means the SDK.
- `--jsonfile` points at a `global.json` and takes `sdk.version` from it.

`--dry-run` resolves without installing, and its output is the cheapest way to check that
our own URL resolution agrees with Microsoft's. Measured:

```
$ ./dotnet-install.sh --channel LTS --dry-run
dotnet-install: Payload URLs:
dotnet-install: URL #0 - aka.ms: https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.302/dotnet-sdk-10.0.302-linux-x64.tar.gz
dotnet-install: Repeatable invocation: ./dotnet-install.sh --version "10.0.302" --install-dir "/home/jason/.dotnet" --architecture "x64" --os "linux"
```

Now the reasons not to run it.

**It does not verify what it downloaded.** Grepped the whole script: no `sha512`, no
`sha256`, no `checksum`, anywhere. All it does is compare the byte count it was told to the
byte count it got, and on a mismatch it prints "The local package may be corrupted" and
**keeps going**. Line 614, measured. Microsoft signs the script itself with GPG and
publishes `dotnet-install.sig`, so the script's own integrity is coverable, but nothing
covers the 218 MB it fetches.

**It resolves no dependencies.** It says so on the way out. On a manual install the
distribution's own libraries are the caller's problem, and the list moves per release:
Ubuntu 26.04 wants `libicu78` and `libbrotli1`, 25.10 wants `libicu76` without brotli,
22.04 wants `libicu70` and `libssl3` rather than `libssl3t64`. Any manual install inherits
this, ours included.

**It needs a shell and a downloader.** Bash, plus curl or wget, one of which has to be
present. The PowerShell one needs an execution policy loose enough to run it. Both are one
more moving thing to keep current in a repository that has none.

Everything the script does that is worth having is thirty lines of managed code over
`HttpClient`, `System.Formats.Tar` and `ZipFile`, and doing it ourselves gets the hash check
the script skips.

## What Kitbash should do

Two lanes. Only one of them ever writes to the machine.

### Lane one, the one that runs: a private install

Resolve a version from the metadata, download the explicit URL, check the SHA-512 that came
with it, extract into a directory Kitbash owns, and never touch PATH, root, the package
manager or anything outside that directory.

Where it goes is `IUserDirectories`. Not the state directory, because this is neither
configuration nor something the app remembers, and deleting state is documented as
resetting Kitbash without taking anything a person chose. An SDK is a large acquired
artifact, so either the cache directory or a fourth kind. That is a decision, and
`ApplicationPaths` is where it lands.

Do **not** install into `~/.dotnet`. That is the install script's default and a person may
well have used it. Sharing that folder means Kitbash and a person's own script writing
the same unversioned `dotnet` binary at each other.

What this buys, against every one of the project's three questions:

1. The other OS differs only in the archive format, `.tar.gz` against `.zip`, and the
   directory. Both are already per OS concerns behind `IUserDirectories`.
2. The other distribution does not enter into it. No feed, no package name, no version
   table, no `/etc/os-release`, and Garuda works for the same reason Ubuntu does.
3. Nothing is there, and it stays that way. No root, no prompt, no lock contention, no
   chance of breaking a system `dotnet` that something else on the machine depends on.

The cost is a copy of the SDK per machine, roughly 218 MB for linux-x64 as measured on the
10.0.302 download, and no participation in system updates. Both are acceptable for
something a tool uses and a person did not ask for.

Pointing at it afterwards is `DOTNET_ROOT` plus the absolute path to the muxer on every
child process, which the integration below does anyway. `global.json` gained a `paths`
array in the .NET 10 SDK, with `$host$` meaning wherever the running `dotnet` came from,
and it is worth knowing about:

```json
{ "sdk": { "version": "10.0.100",
           "paths": [ ".dotnet", "$host$" ],
           "errorMessage": "Run Kitbash to install the SDK this workspace needs." } }
```

That makes a private install satisfy a person's own terminal too, not just ours. It only
works for commands that engage the SDK, so `dotnet run` yes and a published apphost no.
Writing into a workspace's `global.json` is a change to a file a team shares, so it is an
offer and never a side effect.

### Lane two, the one that only talks: the person's own package manager

When .NET is already on the machine and a package manager owns it, Kitbash should say so
and say what the command would be, and stop there.

Running it means root. On Linux that is `pkexec` if polkit is there and `sudo` in a terminal
if it is not, and neither is something to guess at from a GUI. On Windows it is a UAC prompt
through `ShellExecute` with the `runas` verb, which the existing `ProcessRunner` cannot
express, since it is `UseShellExecute` plus a verb and reading the output at the same time
is not possible. And the result of running it is the one documented way to break a Linux
install, by pulling from a second feed.

Say it plainly instead. "This machine's .NET came from pacman. Run `sudo pacman -Syu
dotnet-sdk` to update it." Copyable, correct, and the person keeps the decision. That is
the same shape as `DesktopLauncherResolver` reporting what it looked for.

Which manager owns it, measured on this machine:

```
$ readlink -f $(which dotnet)
/usr/share/dotnet/dotnet
$ pacman -Qo /usr/share/dotnet/dotnet
/usr/share/dotnet/dotnet is owned by dotnet-host-bin 10.0.10.sdk302-1
```

`dpkg -S` and `rpm -qf` are the same question elsewhere. That is a small probe list, not a
matrix, because a miss just means the lane is not offered.

### What version

Not "the latest". The version is whatever the **workspace** needs, and only that question
has a right answer.

1. A `global.json` at or above the workspace root names it, with its own roll forward rules.
2. Failing that, the `TargetFramework` of the project being built names a runtime, and the
   SDK has to be at least that major.
3. Failing both, the newest `active` LTS in the index, which today is 10.0.

`global.json` matters more than it looks. `sdk.version` must be a full three part version,
and `rollForward` defaults to `patch` when a version is given and to `latestMajor` when the
file is absent. The nine values are `patch`, `feature`, `minor`, `major`, `latestPatch`,
`latestFeature`, `latestMinor`, `latestMajor` and `disable`. Reimplementing that resolution
is a mistake. Ask `dotnet` and read what it says, which is the next section.

The support status question is separate and comes from the index. A workspace on an SDK
whose channel is `maintenance` or `eol`, or whose channel's latest release has
`"security": true` above the installed patch, is worth a word in the UI. That is the same
shape as the git strip: read a fact, show it, do not act on it.

The SDK has started answering this itself. `NETSDK1238`, `1239` and `1240` warn at build
time about a vulnerable, end of life or discontinued SDK. They are opt in behind the
`CheckSdkVulnerabilities` MSBuild property, they read a cache under
`~/.dotnet/sdk-vulnerability-cache/` that the CLI refreshes in the background at most daily,
and the build itself makes no network call. `DOTNET_SDK_VULNERABILITY_CHECK_DISABLE` turns
off both halves.

## Finding and reading an installation

This is the part the integration needs whether or not anything is ever installed, and it is
where the surprises were.

### Cost, measured

Five runs each, on a warm machine, wall clock around `Process.Start` to exit.

| Command | Fastest | Slowest |
|---|---|---|
| `dotnet --list-sdks` | 1.5ms | 2.1ms |
| `dotnet --list-runtimes` | 1.3ms | 1.7ms |
| `dotnet --version` | 80.7ms | 95.0ms |
| `dotnet --info` | 147.7ms | 151.3ms |
| `dotnet sdk check` | 622ms | (one run, hits the network) |
| `git --version` | 0.9ms | 1.3ms |

A hundredfold. `--list-sdks` and `--list-runtimes` are answered by the native host and
never start the managed CLI. `--version` loads the SDK and resolves `global.json`. `--info`
loads the SDK and the workload manifests on top.

So: **`--list-sdks` and `--list-runtimes` are the detection commands, and `--info` is not.**
Microsoft's own documentation suggests `--info` for this and it is the wrong advice for
anything running on a schedule. `--info` is for a diagnostics pane a person opened.

Note .NET 11 preview 7 turns on `DOTNET_CLI_ENABLEAOT` by default, which routes
`--version` and `--info` through a native fast path. That should close the gap. It is not
closed today and 10.0 is the LTS.

### The formats

```
$ dotnet --list-sdks
8.0.129 [/usr/share/dotnet/sdk]
9.0.119 [/usr/share/dotnet/sdk]
10.0.302 [/usr/share/dotnet/sdk]

$ dotnet --list-runtimes
Microsoft.AspNetCore.App 10.0.10 [/usr/share/dotnet/shared/Microsoft.AspNetCore.App]
Microsoft.NETCore.App 8.0.29 [/usr/share/dotnet/shared/Microsoft.NETCore.App]
Microsoft.NETCore.App 9.0.18 [/usr/share/dotnet/shared/Microsoft.NETCore.App]
Microsoft.NETCore.App 10.0.10 [/usr/share/dotnet/shared/Microsoft.NETCore.App]
```

`{version} [{directory}]` and `{name} {version} [{directory}]`. Neither is a documented
stable format the way git's porcelain v2 is, which is the honest difference between this
and the git reader. It has held since .NET Core 2.1 and the .NET Install Tool parses it, so
it is what everyone uses. Parse from the ends rather than by splitting on spaces: version is
up to the first space, directory is between the first `[` after it and the final `]`.

### The three things that surprised

**`--version` fails when `global.json` cannot be satisfied, and `--list-sdks` does not.**
Measured, with a `global.json` asking for 7.0.100 and `rollForward: disable`:

```
$ dotnet --version
(stderr) The command could not be loaded, possibly because: ...
         Requested SDK version: 7.0.100
         global.json file: .../gjtest/global.json
(stdout) 8.0.129 [/usr/share/dotnet/sdk]
         9.0.119 [/usr/share/dotnet/sdk]
         10.0.302 [/usr/share/dotnet/sdk]
exit=155

$ dotnet --list-sdks
8.0.129 [/usr/share/dotnet/sdk]      exit=0
```

Three things fall out of that. Exit **155** is the SDK resolution failure and it is worth a
named constant. The error goes to stderr and the **installed list still comes out on
stdout**, so the failure path hands over exactly what is needed to say which SDK is missing
and what is there instead. And `--list-sdks` ignores `global.json` entirely, which is why it
is the right detection command and also why it cannot answer "will this workspace build".

Both questions are wanted. `--list-sdks` from anywhere for what exists, and something run
**in the workspace directory** for whether it resolves. `ProcessRequest.CommandIn` already
carries that, and the note on it, that a program asked about a directory should be run in
it, is exactly right here, because `global.json` is found by walking up from the working
directory.

**`dotnet build -getProperty:` returns clean JSON.** No build, no log parsing, no binlog.
Measured on a fresh classlib with output redirected:

```
$ dotnet build -getProperty:TargetFramework -getProperty:OutputPath
{
  "Properties": {
    "TargetFramework": "net10.0",
    "OutputPath": "bin\\Debug/net10.0/"
  }
}
exit=0
```

That is how the integration should ask a project anything, and it is how the assembly path
for the schema reading in `CLAUDE.md` should be found rather than by guessing at
`.godot/mono/temp/bin/Debug/`. One property gives a bare value, two or more give the JSON
object, so always ask for at least two. `-getItem:` and `-getTargetResult:` are the same
shape.

**`dotnet sdk check` exists, works, and is not parseable.** 622ms, exit 0, a formatted
human table of every SDK and runtime with a support status column, and a network call. No
machine readable form. Its content is exactly what the release index gives in 7 KB with no
process at all. Use the index.

### Where a dotnet is

Ask PATH first, the way `IExecutableFinder` already does for git, and remember that
`PATHEXT` handling on Windows is already solved there. Then the places PATH misses:

| | Linux | Windows |
|---|---|---|
| Ours | the Kitbash directory | the same |
| Environment | `$DOTNET_ROOT` | `%DOTNET_ROOT%`, `%DOTNET_ROOT_X64%` |
| Distribution feed | `/usr/lib/dotnet` | |
| Microsoft feed or upstream | `/usr/share/dotnet` | `C:\Program Files\dotnet` |
| Per user script install | `~/.dotnet` | `%LocalAppData%\Microsoft\dotnet` |
| Arm64 host, x64 SDK | | `C:\Program Files\dotnet\x64` |
| Snap | `/snap/dotnet-sdk-{nnn}/current/usr/lib/dotnet` | |

That is a candidate list, first one present wins, and a message that names what was tried
when none are. Same shape as `DesktopLauncherResolver`, and it belongs behind an interface
with one implementation per OS, chosen in `KitbashCoreServices`, because it is a
directory layout and that is the rule.

`DOTNET_ROOT` was set to `/usr/share/dotnet` on this machine by the Arch package, so it is a
real signal and not a rarity. Note the documented subtlety: it is consulted **only** by
generated executables resolving a runtime, not by the muxer picking an SDK, so it says where
an install is and not which one `dotnet` will use.

`DOTNET_HOST_PATH` is set by the SDK for child processes it starts. Kitbash is not one of
those, so it will not be set for us. If it ever is, honour it, since it means we are running
inside somebody's build.

## The integration

`Kitbash.Core/DotNet`, registered by `AddKitbashDotNet`, mirroring
`Kitbash.Core/Git`. Same reasoning as the git reader, and the same sentence applies word
for word: no library, running the tool works with whatever the person has, honours their
config, and cannot disagree with what they see in a terminal.

Suggested shape.

- **`IDotNetLocator`** finds every installation. PATH through `IExecutableFinder`, then the
  candidate table, then `readlink` or its equivalent to collapse duplicates. Returns
  `DotNetInstall` records: muxer path, root, and where it appears to have come from.
- **`IDotNetReader`** runs `--list-sdks` and `--list-runtimes` against one muxer and parses
  them into a `DotNetInventory`. Two runs, three milliseconds, cacheable per muxer path with
  the muxer's own last write time as the key.
- **`IDotNetResolver`** answers the workspace question by running in the workspace root and
  reading exit 155 plus stdout, so `global.json` resolution stays Microsoft's to implement.
- **`IDotNetReleases`** reads `releases-index.json`, caches it under
  `ApplicationPaths.CacheFileFor`, refreshes no more than daily, and works from the cache
  when offline. It is the only piece here that touches the network.
- **`IDotNetRunner`** runs a command in a workspace with the environment pinned. Everything
  above except the releases reader goes through this.
- **`IDotNetInstaller`** is lane one, and nothing else needs it.

There is no equivalent of `IGitStatusMonitor`. Nothing about a .NET install changes minute
to minute, so a read when a workspace opens and a read after an install is the whole thing.
Do not watch a directory for this.

### The environment to pin on every run

Set through `ProcessRequest.With`, which already exists and already documents that an empty
value unsets rather than blanks.

| Variable | Value | Why |
|---|---|---|
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | a person did not choose to run this |
| `DOTNET_NOLOGO` | `1` | the first run banner otherwise lands in the first thing parsed |
| `DOTNET_CLI_UI_LANGUAGE` | `en` | anything parsed has to be in a known language |
| `MSBUILDTERMINALLOGGER` | `off` | belt and braces, see below |
| `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE` | `true` | stops a background manifest download we did not ask for |
| `DOTNET_ROOT` | our root | only when running our own install |

`DOTNET_CLI_UI_LANGUAGE` is the one with a cost. It pins the language of anything a person
might be shown, so it belongs on commands whose output is parsed and not on a command whose
output is handed to them. Splitting those two is worth doing once rather than discovering
later.

The terminal logger is on by default for `dotnet build` since .NET 9 and it is not
parseable. Microsoft says it turns itself off when output is redirected, which
`ProcessRunner.ReadAsync` always does, so it should never appear. It was not observed in the
`-getProperty` run. Set the variable anyway, since the alternative is relying on a heuristic
inside somebody else's tool to notice something about our pipes.

### Reusing what is already there

Almost all of it. `IProcessRunner` reads both pipes before waiting and kills the process
tree on cancellation, which is what a long `dotnet build` or a 218 MB download needs.
`ProcessRequest.CommandIn` runs in a directory, which is how `global.json` gets found.
`IExecutableFinder` already handles `PATHEXT`. `ProcessStartException` already covers the
program vanishing between the lookup and the run.

Two things are missing.

**No cancellation deadline.** `GitUpdater` sets a 30 second limit itself. A restore or a
build has no sensible upper bound and a download has a different one again, so a timeout
belongs on the request or on each caller rather than being invented per class.

**No streaming.** `ReadAsync` returns everything at the end. A build that runs for two
minutes with nothing on screen is worse than no integration, and progress on a 218 MB
download is not optional. That is a real addition to `IProcessRunner`, an overload that
raises a line as it arrives, and it is the only change to existing code this work needs.

### Version comparison

Do not use `System.Version`. SDK versions carry prerelease labels, `11.0.100-preview.6.26359.118`
is a real one from the index today, and `Version.Parse` throws on it. Feature bands mean
`10.0.302` is newer than `10.0.100` in a way that only matters if the band is understood.

`NuGet.Versioning` would solve it and would be a second dependency in a project whose only
one is Tomlyn, which `CLAUDE.md` says to keep that lean. So a small `SdkVersion` value type,
parsed, comparable, with `Major`, `Minor`, `FeatureBand`, `Patch` and an optional
prerelease, the way `WebAddress` and `DirectoryLocation` are value objects that cannot be
made unchecked.

## Measured, in one list

Everything here was run on this machine unless it says otherwise.

1. `dotnet --list-sdks` is 1.5ms and `dotnet --info` is 148ms, because the first never
   starts the managed CLI. Use the first.
2. A `global.json` that cannot be satisfied makes `dotnet --version` exit **155**, write the
   error to stderr, and still write the installed SDK list to stdout.
3. `dotnet --list-sdks` ignores `global.json` completely and exits 0 in the same directory.
4. `dotnet build -getProperty:A -getProperty:B` prints a JSON object and nothing else, with
   output redirected, exit 0. One property alone prints a bare value.
5. `dotnet-install.sh` performs no hash check of any kind. It compares byte counts, and on a
   mismatch it prints "may be corrupted" and continues. Script line 614.
6. `dotnet-install.sh` needs bash plus curl or wget, and honours exactly one environment
   variable, `DOTNET_INSTALL_DIR`.
7. `--dry-run` resolves the concrete version and prints a repeatable invocation, which is a
   free way to check our own resolution against Microsoft's.
8. `aka.ms/dotnet/11.0/dotnet-sdk-linux-x64.tar.gz` returns **200** and a bing.com search
   page. An unknown alias does not 404.
9. `releases-index.json` is 7 KB and answers the support and update questions.
   `10.0/releases.json` is 935 KB and is only needed for a URL and a hash.
10. Every file in the channel metadata carries a SHA-512, and
    `builds.dotnet.microsoft.com/dotnet/checksums/{release}-sha.txt` exists as well.
11. Both metadata files carry a detached `.p7s` signature, and it is fetchable.
12. This machine's `/etc/os-release` has `ID=garuda`, `ID_LIKE=arch` and **no `VERSION_ID`**,
    so a distribution matrix keyed on name and version cannot see the development machine.
13. Microsoft's own distro matrix, `distro-support.json` in the .NET Install Tool, covers
    three distributions and falls back to a user local install for everything else.
14. `packages.microsoft.com` no longer publishes .NET for Ubuntu, Fedora, RHEL, CentOS
    Stream or Alpine. Since .NET 9 it covers only distributions that publish none of their own.
15. `dotnet sdk check` works, takes 622ms, hits the network, and has no machine readable form.
16. The Arch package sets `DOTNET_ROOT=/usr/share/dotnet`, so the variable is in normal use
    and not an edge case.

## Not tested here

This machine is Linux. Everything below is documentation and needs a Windows machine before
anything is written against it.

- Every winget package identifier, and whether `winget install --silent` returns something
  useful when the package is already current.
- The exe installer's exit codes in practice, particularly 3010 for a wanted restart.
- Whether `RemovePreviousVersion` defaulting to `always` really does take a patch a
  `global.json` pinned, which is the one behaviour here that could break a workspace.
- The x86 first on PATH failure, and whether `--list-sdks` reports usefully when it happens.
- `%LocalAppData%\Microsoft\dotnet` as the script's default, and the Arm64 `\x64` subfolder.
- Elevation through `ShellExecute` with the `runas` verb, which the current `ProcessRunner`
  cannot express at all.

Not tested on Linux either, and worth a look before lane two ships:

- `pkexec` against `sudo` in a real desktop session, and what happens with polkit absent.
- Whether a Flatpak or Snap packaged Kitbash can see the host's `dotnet` at all. Neither
  marker was present here, `/.flatpak-info` is absent and `$SNAP` is empty, so the sandbox
  case is entirely untested. Given that a sandbox moves every user directory, lane one's
  private install is probably the only lane that works there, which is another point in its
  favour.

## Decisions this leaves open

Do not assume any of these.

- Where a private SDK lives. Cache, or a fourth kind of application directory beside
  configuration, state and cache. It is large, it is acquired rather than authored, and
  deleting it should cost a download and nothing else.
- Whether Kitbash ever writes `global.json` into a workspace. It is the mechanism that
  makes a private install work for a person's own terminal, and it is a file a team shares.
- Whether an update is ever offered for a system install, or whether lane two stays purely
  informational forever.
- What actually needs the SDK. The schema reading in `CLAUDE.md` reads
  `.godot/mono/temp/bin/Debug/Slopworks.dll` through `MetadataLoadContext`, and that file is
  a build output. If Kitbash is meant to produce it rather than wait for it, the SDK is a
  hard requirement and this becomes load bearing. If a committed schema manifest is used
  instead, as `CLAUDE.md` prefers, then none of this is on the critical path and it is a
  convenience.
