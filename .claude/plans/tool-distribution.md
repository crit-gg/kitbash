# Tool distribution

How a tool reaches a machine, where it lives, and how it is kept current.

The two tier model this sits inside is the `kitbash-updates` skill: Velopack installs and
updates the launcher, and the launcher installs and updates tools. An earlier version of
this assumed one catalogue on one server we run. Sources are plural instead, and the first
one is GitHub releases.

## Status

**Steps 1 to 6 of the order of work are built.** A tool is found, listed, started,
offered by a repository, installed, updated and uninstalled, and the global repository
list is edited in the settings window. **Step 7, the GitHub login, is the only one left**,
so a private repository is not reachable and the hourly unauthenticated request allowance
is what a check costs.

**Install from folder is built as well**, which is outside the seven steps. A folder
holding a manifest is a tool where it sits, under a `local.` id, and nothing is copied.
See A folder on this machine is the other way in.

**A tool can also be a script**, which is outside the seven steps too. The manifest says
`"kind": "script"` and declares what to ask for, and the launcher runs it under a modal
rather than letting it go. That is manifest format 2 and it is the whole of what format 2
added. `.claude/plans/tool-scripts.md` has it.

- **The Windows user directories moved.** State and cache are under
  `%LOCALAPPDATA%\KitbashData` and the install root holds the app alone.
- **`Kitbash/Tools/` is the model**, and it is the launcher's rather than Core's:
  `ToolId`, `ToolVersion`, `ToolManifest`, `IToolManifestReader`, `IToolRuntime`,
  `IInstalledTools`, `IToolStarter`, `IToolRepositoryList`, `IToolRepository` with
  `GitHubToolRepository`, `IToolCatalogue`, `IToolInstaller` and `ToolLog`.
- **`ITool`, `IToolActivation`, `IToolRegistry`, `ToolDescriptor` and
  `DelegateToolActivation` are gone from Core**, along with `App.BuildRegistry` and
  `MockToolCatalogue`. Core keeps `SettingsScope.ForTool`, which is only a string.
- **Core grew two things this needed.** `IWorkspaceSettingsFactory`, so an app holding a
  list of workspaces can read any of them, and `IFileSystem.MoveDirectory`, which is what
  puts a version directory in place whole.
- **The tools page is the real thing.** Installed and available are two groups, Install,
  Update, Update all, Check for updates and Uninstall all do what they say, and the card
  draws its progress from the install.

Measured on this machine, Linux, through a console harness and a headless run of the real
launcher. What was exercised, with a real download over http, a real checksum and a real
archive: a tool placed by hand found and started; a repository listing, offering the
newest version, skipping a prerelease and a tag that is not a version; an install from a
card's Install button; an update from its Update button, with both versions on disk and
the older one swept at the next launch; an uninstall keeping `state.toml`; a payload one
byte short and a payload one byte changed both refused with nothing left behind; a second
repository claiming a taken id refused by name; and two unreachable repositories changing
nothing on screen while the installed tool kept working. The GitHub release parse was
checked against the live API on two real repositories.

**What has not been exercised: a real Kitbash tool release.** No tool is published, so
the release list and the manifests came from the launcher's own caches, written by hand
in the shape GitHub answers, and the payloads came from a local http server. The parse of
GitHub's real JSON was checked separately against `velopack/velopack` and
`godotengine/godot`.

## Settled

- **A tool comes from a repository, and repositories are plural.** `IToolRepository` is
  the seam. GitHub releases is the first implementation.
- **Repositories are listed in two places.** The global config offers a repository
  everywhere. A workspace's config offers one in that workspace alone.
- **A workspace holds a repository list and nothing else.** No payload, no install
  record, no version.
- **A tool is installed once for the machine, at one version.** Where it came from does
  not change that.
- **Nothing installs or updates itself.** A person clicks Install, and a person clicks
  Update.
- **An id carries its source.** The manifest says `foundry` and the launcher stores
  `github.foundry`.
- **A collision refuses all but the first.** Two repositories claiming one id means the
  second is not offered, with a message naming the holder.
- **One repository, one tool.**
- **The tag is the version.** A release is a version, and the head of a branch is never
  one.
- **A version with no payload for this platform is not offered and is not an update.**
  Hidden rather than shown disabled.
- **A repository that cannot be reached changes nothing on screen.** Installed tools are
  unaffected and uninstalled ones are absent.
- **An update can be required, and the manifest says so.** Required means the tool will
  not start until somebody clicks Update, never that anything installs itself.
- **Nothing a person owns lives in the folder Velopack deletes.**

## Where a tool comes from

### The abstraction is two operations

```
list the versions this repository offers
fetch a named file for one version
```

Resisting a third is the discipline. Everything the launcher does is built out of those
two, so a repository type is small enough that adding one is an afternoon.

### GitHub releases, first

A release is a version. The tag names it. The payload and the manifest are assets on it.

One call to the releases API lists every version with its assets together, which is why
this type answers both operations from a single request. Public assets download from
`https://github.com/<owner>/<repo>/releases/download/<tag>/<name>` with no API involved.

**It is named `github` and not `git`, because a release asset is not a git object.** No
git command can fetch one. Calling the type `git` would put a generic name over one
forge's API and mislead the next person who tries to point it at a plain repository.

### What comes later, and why it is different

**A plain git repository** can list versions, since `git ls-remote --tags` works against
any host over http or ssh and uses the person's own git and credential helper. It cannot
fetch a release asset, because there is no such thing outside a forge. So a git type has
to carry its payload in the repository and install by checking out a tag, which is a
different model with different costs, not a variation on this one. Build it when a host
that is not a forge actually matters.

**A static index** on a bucket is the cheapest of the three, and it was the original
arrangement here: `tools.json` beside the launcher's own feed. It is worth having for
anything published alongside the launcher.

**Gitea and GitLab** are the same shape as GitHub with different URLs.

## What a tool repository has to look like

This is the contract a tool author satisfies. It is the whole of it.

### One repository, one tool

A repository holding several tools needs a tag prefix per tool, and every tool in it
releases together. If that is ever wanted it is a prefix rule added then.

### A release per version

The tag is a semantic version with an optional leading `v`. A release marked prerelease
maps to a prerelease version and is not offered until a preview channel exists.

**A tag that does not parse is skipped rather than failing the repository.** A repository
with unrelated tags in its history is ordinary.

### The assets

| Asset | What it is |
|---|---|
| `kitbash-tool.json` | the manifest, at exactly this name |
| whatever the manifest names | one payload per platform |
| whatever the manifest names | the icon, optional |

The fixed manifest name is what lets the launcher find it without guessing. Everything
else is named by the manifest, so a tool's build can call its archives anything.

**The launcher fetches only the manifest to draw a card.** A few hundred bytes tells it
the name, the summary, whether this platform has a payload at all, and how large that
payload is. Nothing large is downloaded until somebody clicks Install.

**A release asset never changes, so a manifest caches forever** under the version it came
from. Only the list of versions is ever refetched.

### The manifest

```json
{
  "manifest": 1,
  "id": "foundry",
  "name": "Foundry",
  "summary": "Data editor for attributes, stats, effects, machines and recipes.",
  "category": "Content",
  "icon": "foundry-icon.png",
  "version": "0.10.0",
  "payloads": [
    {
      "runtime": "linux-x64",
      "asset": "foundry-0.10.0-linux-x64.tar.gz",
      "size": 104857600,
      "sha256": "...",
      "executable": "Foundry"
    },
    {
      "runtime": "win-x64",
      "asset": "foundry-0.10.0-win-x64.zip",
      "size": 109051904,
      "sha256": "...",
      "executable": "Foundry.exe"
    }
  ]
}
```

**JSON rather than TOML**, which is the one place this repository departs from its own
habit. A manifest is written by a build rather than by a person, and a tool need not be
.NET, so its author has to emit it with nothing installed. The rule that falls out is
worth keeping: TOML is what a person edits, JSON is what programs exchange.
`System.Text.Json` is already in the box, so this adds no package.

`manifest` is a plain integer and the only version here that is not semantic. It
describes a file format. The launcher reads it before anything else and it is what makes
a newer tool refusable rather than mysterious. **This launcher reads up to 2**, and format
2 is what added `kind` and `inputs` for a script tool.

**`version` must equal the tag.** A release where they disagree is refused rather than
guessed at, because the two would then have to be reconciled at every later comparison.

`icon` is optional. Without one the card draws the letter mark.

**It is built.** `IToolIcons` is the whole of it. The name is a plain file name beside the
manifest, and the reader drops anything else, so a name carrying either separator or an
extension nothing can draw leaves the tool with its letter rather than refusing the
manifest. An install fetches it into the version folder, which is what lets an installed
tool draw itself offline, and a tool that is only offered has its icon fetched into the
cache instead, keyed by repository, tag and name, so it is fetched once ever. A public
asset downloads off the release url with no API involved, so this costs no request
allowance. The download is stopped at 4 MB, since nothing declares an icon's size and
nothing hashes it. Every failure is logged and swallowed: the card keeps its letter and an
install that otherwise worked still works.

A tool installed before it published an icon has none in its folder, so the card falls
back to the offer's until the next update, and nothing has to be reinstalled to gain art.

The tile clips the icon to its own rounded corner with a second border inside the one that
draws the stroke, which is the rule in `.claude/avalonia.md`, and `RadiusSurfaceInside` is
the token for it. `ToolIconTileTests` renders the real card template inside the real
launcher window and reads the four corner pixels back.

`executable` is a path inside the payload, and it is what gets run.

**`required` is optional and defaults to false.** A version carrying it refuses every
version below it. See Updating.

**`runtime` may be `any`**, which is a payload the author says runs on every platform.
A script is the case that needs it, and without it nothing but a .NET tool could publish
here. It is the author's claim and the launcher takes it at face value, so a payload
holding a shell script and marked `any` will be offered on Windows and fail to start
there. **A concrete match wins over `any`**, so a tool may ship a portable payload and
replace it on the platforms where it has something better.

## The repository lists

```toml
[[tools.repositories]]
type = "github"
url = "https://github.com/owner/foundry"
```

The same shape in the global config and in a workspace's. **The type is written rather
than inferred**, because a self hosted Gitea URL and a plain git URL look identical and
guessing wrong is the thing the abstraction exists to prevent. `ToolRepositorySource.Kinds`
is what the settings window offers, and a file naming a type that is not there keeps it,
so a list written for a newer copy survives being saved by this one.

**Availability is derived rather than stored.** A tool is offered in a workspace when its
repository is in the global list or in that workspace's list. Nothing records which
workspace a tool belongs to, two workspaces naming one repository both see it, and
dropping the entry stops it being offered without touching what is installed.

**A workspace config travels in a clone**, so a repository list can name a program nobody
in the room chose. That is safe because offering costs nothing and the install button is
the only way in. **The card names the repository it came from**, so the person clicking
has been told.

**A tool no global list offers is marked on its card.** A small mark at the top right,
whose tooltip names every workspace providing it, so a tool that travels with a workspace
is told apart from one offered everywhere. It reads every registered workspace's list
rather than the open one alone, since a repository two workspaces share is offered by
both and a person switching workspaces keeps it. A workspace naming a different
repository that happens to hold the same tool is not one of them, because knowing that
would cost a request per workspace. `ToolRepositorySource.Workspace` is what carries it,
null for the global list, and `OfferedTool.Workspaces` is the set the tooltip reads.

### Ids carry their source

The manifest says `foundry`. The launcher stores `github.foundry`. A manifest author does
not know how their tool will be consumed, so the qualification is the launcher's to add.

The id is a file name, which `SettingsScope.ForTool` already enforces, and
`github.foundry` is a valid one on both operating systems.

**A collision refuses all but the first discovered.** For that to mean the same thing
twice running, discovery has a defined order:

1. **An installed tool owns its id permanently.** Whatever is on disk wins, whatever the
   lists say now.
2. Then the global list, in the order the file writes it.
3. Then each workspace list, in the order the file writes it.

The refusal is visible rather than a tool quietly missing, and it names the repository
holding the id.

## Where a tool lives

```
<state>/tools/<id>/<version>/
```

Which is `$XDG_DATA_HOME/kitbash/tools/` on Linux. `IUserDirectories.StateFor` already
resolves to the data directory there rather than the state directory, deliberately, so
this is the same place engines already use.

### Windows has to move first

`WindowsUserDirectories` returns `%LocalAppData%\Kitbash\State` and
`%LocalAppData%\Kitbash\Cache`. The Velopack pack id is `Kitbash`, so
`%LocalAppData%\Kitbash` is the install root, **and its uninstaller deletes that whole
directory**. Adding tools under it would mean uninstalling the launcher silently removes
every tool a person installed, on top of the engines and the workspace list it already
takes.

**The pack id cannot change.** It names every download, including `Kitbash.AppImage` and
`Kitbash-win-Setup.exe`, and the redirects on download.kitbash.run point at those names.
Commit 603430e renamed it for exactly that reason and recorded moving the user
directories as the fix.

**So the user directories move, and Velopack's folder holds the app alone.**

| | Before | After |
|---|---|---|
| Install root | `%LocalAppData%\Kitbash` | unchanged, app only |
| Configuration | `%AppData%\Kitbash` | unchanged, already outside |
| State | `%LocalAppData%\Kitbash\State` | `%LocalAppData%\KitbashData\State` |
| Cache | `%LocalAppData%\Kitbash\Cache` | `%LocalAppData%\KitbashData\Cache` |

Linux does not change. Nothing has been distributed, so there is no migration to write,
and **this has to land before the tools directory exists** rather than after. **It has
landed.**

`KitbashData` is the one arbitrary name here. It exists because the plain one is taken,
and it is free to change until the first release.

### The tools directory already means something else

`ApplicationPaths.StateFileFor` writes per tool state to `<state>/tools/<id>.toml`. Put
installs at `<state>/tools/<id>/<version>/` and that one directory holds both
`github.foundry.toml` and `github.foundry/`, which is two purposes in one place.

**Fold the state file into the tool's own folder instead.**

```
<state>/tools/<id>/state.toml      what the launcher remembers about this tool
<state>/tools/<id>/<version>/      one directory per installed version
```

Everything about one tool sits together, and uninstalling deletes the version directories
while leaving the state, which is what makes reinstalling free. Settings are untouched,
since those live under Configuration and never held an install.

## A repository that cannot be reached says nothing

**No message, no badge, no error.** A repository that fails to answer changes nothing a
person sees.

- **An installed tool is unaffected.** It keeps its version, it keeps starting, and its
  card looks the same. What is on disk is the truth about what runs, and a server having
  a bad day is not.
- **A tool that is not installed is absent.** It does not appear in the available list,
  the same as a repository that offered nothing.

So an unreachable repository and an empty one look identical, on purpose. **The log is
the only place a failure is recorded**, which keeps it diagnosable without putting a
network problem in front of somebody who did not cause it and cannot fix it.

## A version with no payload for this platform does not exist

**Not offered, not shown disabled, not explained.** A tool with no payload for this
machine never reaches the available list, and a new version with no payload for this
machine is never an update.

**The filter is per version, not per tool.** A tool can carry a Linux payload at 1.0 and
drop it at 2.0, so what a person is offered is the newest version having a payload for
this runtime identifier, and versions above it are simply not there.

A payload marked `any` counts as a match, and a payload naming this runtime identifier
beats it when both are present.

That means the version list is filtered before the newest is picked. Walk back from the
newest, fetching each manifest until one matches, and stop after a bounded number so a
repository that never matches costs a fixed amount rather than its whole history.
Manifests cache forever, so this is paid once per version ever seen.

## Installing

A person clicks Install. Then:

1. Read the manifest for the chosen version. Refuse a `manifest` newer than this launcher
   understands, and say which.
2. Find the payload for this runtime identifier. A version reaching here always has one,
   so its absence is a bug rather than a message.
3. Download to a temporary file.
4. Check the size and the SHA256 against the manifest. A mismatch deletes the file and
   reports it.
5. Extract to `<version>.incoming`.
6. Set the executable bit on the file `executable` names, on Unix.
7. Rename `<version>.incoming` to `<version>`.
8. Write the active version into `state.toml`.

**Nothing in use is ever overwritten**, so there is no partly updated state to recover
from. A version directory exists whole or does not exist. A failed download leaves a
temporary file and nothing else.

**tar.gz for Unix payloads, zip for Windows.** A zip carries a unix file mode in the top
sixteen bits of an external attributes field, and .NET writing a zip on Windows sets that
to zero, so a Linux payload zipped by a Windows build extracts with no executable bit and
nothing anywhere says why. Step 6 covers the file the manifest names. Tar covers every
helper binary beside it.

### Starting a tool

```
<executable> --workspace <path to the open workspace>
```

Working directory is the version directory, so a tool finds its own files beside it. With
no workspace open the argument is absent.

**A script tool is started differently**, since the launcher waits for it and reads its
output. The workspace argument is the same and the form's answers follow it. See
`.claude/plans/tool-scripts.md`.

**That plus the manifest is the whole contract.** Settings, workspace discovery and how a
tool draws a window are not in it. A .NET tool takes `Kitbash.Core` and `Kitbash.Ui` and
gets them for free, which is why those exist. Anything else reads a path and does as it
likes.

**The launcher can get out of the way once the tool is up.** `launcher.after.tool` is what
every tool follows and `launcher.after.tools` holds the ones that differ, both on the
Launcher settings page. The `kitbash-settings` skill has them. It follows a start that
really happened, so a tool that would not start leaves the launcher up with its toast on
screen. Nothing about it reaches the tool, which is told only which workspace is open.

## Updating

**Checking is not installing.** Applying an update is a click, always. Checking is one
API call per repository and it is the only thing that makes an update visible, so the
launcher checks on its own and never applies.

Cadence: at launch, and no more often than a few hours per repository afterwards. Check
for updates on the page ignores the cache and asks now.

### A required update blocks the tool rather than installing itself

**The manifest says an update is required.** A version whose manifest carries
`"required": true` refuses every version below it. If any version between what is
installed and the newest carries it, the update is required, so a person cannot get past
one by skipping to the version after it.

**Required still never means automatic.** Nothing installs itself, so a required update
means **the installed tool will not start until somebody clicks Update**. The card draws
the blocked state it already has markup for, says why, and offers the update.

A repository that cannot be reached cannot say a version is required, so a tool nothing
has heard about keeps starting. That falls out of the rule below rather than being a
separate case.

**One version per machine.** Two workspaces wanting different majors of one tool is not
solved, and the layout leaves room for it later because several version directories can
sit side by side under an active pointer.

An old version directory is removed after the active version changes, which can happen
later or at the next launch. It never happens while anything might be reading it.

### Rate limits are the reason the login matters

GitHub allows 60 unauthenticated requests an hour per address, and 5000 authenticated.
A handful of repositories checked on a beat runs into 60 quickly. **A conditional request
answered 304 is documented not to count**, which the cadence should lean on, and which
should be confirmed rather than assumed.

## Private repositories

`ISecretStore` already exists and its own example is `SecretKey.Parse("github", "octocat")`,
so a token has a home in the platform keyring on both operating systems. Every operation
can answer `Unavailable`, which is a machine with no keyring, and that has to stay a
defined outcome rather than a crash.

A private repository needs the API for the download as well as the listing, since the
plain `releases/download` URL is not readable without credentials.

## A folder on this machine is the other way in

**Install from folder points Kitbash at a folder and nothing is copied.** A tool author
building their own tool needs it on the page before it is published anywhere, and copying
a publish output on every rebuild is friction that makes the launcher the slow way to run
your own program.

It is not a repository. `IToolRepository` answers two operations, list the versions and
fetch a file for one, and a build output folder has one version and nothing to fetch.
`IToolFolderInstaller` is its own seam, the repository lists are untouched, and the
catalogue never sees it.

**The manifest is still the whole contract.** `kitbash-tool.json` at the folder's root,
the payload matching this machine, and the program `executable` names sitting inside it.
`asset`, `size` and `sha256` are already optional in the reader, so a folder needs none of
them.

**The id is `local.<name>`.** `local` is reserved and no repository type may take it, so a
folder build and a released copy of the same tool are two ids and can both be installed.
Nothing a repository offers ever matches a `local.` id, so a linked tool is never shown an
update.

### What the state file holds

```
<state>/tools/local.<name>/state.toml
  [install]
  directory = "/home/jason/dev/foundry/publish"
```

That folder is where the tool runs, so a rebuild in it is picked up with nothing installed
again. The version comes from the manifest each time it is read, which is what makes that
work.

**A relative path is refused rather than resolved**, since it would mean a different
folder to every process that read it. The path is stored absolute with no trailing
separator.

### What it refuses, all before anything is written

A folder that is not there, one holding no manifest, a manifest whose format is newer than
this launcher reads, a manifest with no payload for this runtime identifier, a manifest
naming a program that is not in the folder, and a folder inside `<state>/tools/`, which is
already listed through the tool it belongs to.

### Removing one deletes nothing

**A linked folder belongs to the person.** Uninstall forgets the path and leaves every
file alone, which is the imported engine rule and the same dialog wording. The card's menu
says Remove rather than Uninstall for exactly that reason.

**A folder that has gone leaves the tool out**, logged and not drawn, the same as a
version directory that will not read. The state entry stays, so putting the folder back
brings the tool back.

**The sweep skips a linked tool.** It deletes every directory under a tool's folder that
is not the active version, and a linked tool's version is somewhere else entirely, so
without the guard the sweep would take anything sitting there.

Measured on this machine, Linux, 30 checks over a console harness against real folders and
real state files, and the page and both dialogs rendered headlessly through the real
launcher window: a folder installed, read back in a fresh process, and its executable
resolving into the picked folder; a rebuild changing the version with nothing reinstalled;
each of the eight refusals; a payload marked `any`; a trailing separator trimmed; the
folder moved away and the tool dropping out while its neighbour kept working; remove
leaving every file where it was; the sweep leaving a linked folder alone; and adding it
again finding it as it was left.

## Uninstalling

Delete the version directories. Keep `state.toml`. Keep the tool's settings under
Configuration, and keep anything under a workspace's `.kitbash/`, which belongs to the
workspace.

**Never delete a person's data because they removed a program.** Reinstalling should find
everything as they left it.

A tool that leaves a catalogue is not uninstalled. **What is installed keeps working**,
because deleting a row on a server must never remove a program from a machine.

## What this changes in the code

- **`App.BuildRegistry` goes.** The registry becomes a scan of `<state>/tools/*/` plus
  whatever the repositories offer.
- **`ITool` stops being an interface anyone implements.** It becomes a record the
  launcher fills from a manifest. `IToolActivation` collapses to starting a process.
- **The tool model moves out of `Kitbash.Core` into `Kitbash`.** Core is a library that
  makes writing a .NET tool easy, not the contract every tool obeys, and only the
  launcher installs anything. Core keeps `SettingsScope.ForTool`, which is only a string.
- **`MockToolCatalogue` is deleted** and `LauncherViewModel.HasTools` stops being false.
  The page already draws installed, available, update offered and blocked, so the markup
  is waiting rather than missing.
- **`ApplicationPaths`** grows the tools directory and changes the shape of
  `StateFileFor`.
- **`WindowsUserDirectories`** moves State and Cache out of the install root.
- **`.claude/CLAUDE.md`** says `Kitbash.Core` is the shared contract and that tools are
  registered explicitly. Both need amending.

## Order of work

1. **Move the Windows user directories.** Everything else writes into them.
2. **The manifest, its reader, and the id rules.** No network.
3. **Discovery from disk.** Scan, list, and delete the mock. A tool folder placed by hand
   is simply a tool, which falls out for free.
4. **Starting a tool as a process.**
5. **`IToolRepository` and the GitHub implementation.** Listing and offering only.
6. **Install, then update, then uninstall.**
7. **The GitHub login**, over `ISecretStore`.

### The cheapest thing that proves it

**A shell script and a manifest, placed by hand in `<state>/tools/hello/1.0.0/`.** Found,
listed, started. No feed, no download, no installer, and deliberately not a .NET program.
If the launcher runs a shell script then it installs and starts programs rather than
hosting managed code, which is the claim the whole two tier model rests on. Steps 1 to 4
are exactly this and nothing more.

**That is what was done, and the claim holds.** The script ran, in its own folder, with
the workspace it was told about. Alongside it: a folder whose name is not an id, one whose
manifest names a different tool, one whose only payload is for another platform, and one
with no version in it, all four left out; three versions of one tool with the newest
opening; and a `state.toml` naming an older one, which opened instead.

Then the same with the gallery, which is a real .NET application, to find out what a
hundred megabyte payload feels like to install.

## What building steps 1 to 4 settled

Everything here is a decision this page did not already hold.

**The manifest is copied into the version folder**, at `kitbash-tool.json`, so
`<state>/tools/<id>/<version>/` holds the payload and the manifest it came from. That is
what lets a card draw its name, summary and executable with no network, and it is what
makes a folder placed by hand a tool with nothing else written anywhere.

**The manifest says which version a folder is, not the folder name.** So the two can never
disagree and nothing has to reconcile them. A folder can be called anything.

**The folder name is the id, and the manifest's `id` has to be its tool half.** A folder
called `github.foundry` holding a manifest that says `foundry` is that tool, and one that
says anything else is skipped rather than opened under a name it did not choose.

**An id is lower case letters, digits and dashes, in one part or two.** Stricter than a
file name, because Windows ignores case and Linux does not, so `Foundry` and `foundry`
would be one tool on one machine and two on the other. One part is a tool with no source,
which is what a folder placed by hand is.

**`state.toml` holds `install.version`, and it is read through `IApplicationState`.** A
tool's scope already points at that file now `StateFileFor` is folded, so nothing new
stores anything. **A version it names that is not installed falls back to the newest that
is**, rather than leaving the tool missing.

**This machine's runtime identifier is `RuntimeInformation.RuntimeIdentifier`.** It is
what .NET reports for the running host, so nothing here tests the operating system, and a
musl machine reports itself as one rather than claiming a payload built for glibc.

**Everything about downloading is optional in the reader**, so `asset`, `size` and
`sha256` may all be absent. A folder placed by hand has nothing to download and still has
something to run. The installer is what requires them, and it is not built.

**The executable is a relative path written with forward slashes**, since that is what an
archive holds whichever platform packed it. A path that is rooted, holds `..`, or carries
a character Windows refuses is refused when the manifest is read, because a manifest comes
off the internet and names a program the launcher then runs.

**A folder that will not read is left out and nothing is said.** Same rule as an
unreachable repository. There is nowhere to log it yet, which is worth fixing when step 5
lands and failures start having causes worth reading.

**Uninstall is on the card's menu and it asks first**, through the same dialog the
engines page uses, which now takes a tool as well as an engine.

## What building steps 5 and 6 settled

**An installed tool does not pre claim its id.** The page has to be able to say a newer
version is waiting, and it cannot if the id being installed stops every repository
offering it. So the catalogue answers one offer per id whatever is installed, first
source in list order wins the id, and the page joins the two by id: installed with a
higher offer is an update, an offer with nothing installed is available. Nothing on disk
is ever touched by a list changing, which is what the rule was protecting.

**The launcher reads the disk, draws, then asks the network and draws again.** A person
who has a tool should not wait on a server to see it. The tools page fills in twice on
every refresh and the second pass is the only one that can be slow.

**A repository type that is not github.com is not this type.** `GitHubToolRepository`
refuses any other host rather than guessing at an API root, so a self hosted forge is a
type of its own when somebody wants one. The factory logs what it could not read.

**An install refuses a payload with no checksum.** The plan's manifest always publishes
one and a payload is a program off the internet, so an unhashed one is refused rather
than trusted. `asset`, `size` and `sha256` stay optional in the reader, since a folder
placed by hand has nothing to download.

**tar.gz or zip is chosen by what the asset is called, not by the platform**, so a
payload marked `any` unpacks the same way everywhere. Nothing is stripped from the front
of an entry path: the manifest names the executable exactly as the archive holds it. Only
plain files are unpacked, so a link inside a payload is skipped rather than followed.

**The release list is cached for three hours and a manifest forever.** Check for updates
passes refresh, which ignores both. Conditional requests, which GitHub documents as not
counting against the allowance, are not built, because `IWebContent` carries no headers.
That is the cheapest thing to do when the login lands.

**An old version is removed at the next launch, never during an update.** The launcher
sweeps once when it reads the tools directory, and a directory Windows will not delete
because something is running from it is left for the launch after.

**The global list is edited in the settings window and a workspace's is not.**
`[[tools.repositories]]` is an array of tables, which the settings machinery could read
but not write, so `TomlDocument` learnt to write one and `SettingsEditorRow` gave a page
somewhere to draw it. The Tool repositories page holds
`Kitbash/ViewModels/ToolRepositoriesEditor`, which writes the global config through
`IToolRepositoryList.WriteGlobal`, and a readout under it lists every repository in force
with where each is listed. **A workspace's own list is only ever read**, since it is a
team file that travels in a clone, so changing it stays a file edit. The `kitbash-settings`
skill has the rules for both halves.

## Open questions

- **Two workspaces on different majors of one tool.** Left unsolved on purpose.
- **A tool needing a newer launcher.** A manifest version this launcher cannot read is
  refused, and the tool is kept and disabled rather than deleted. What is missing is the
  offer of the launcher update that would fix it, since the launcher's own updates are
  still off.

## What could not be tested here

This machine is Linux. Everything about `%LocalAppData%`, the uninstaller and what it
removes is reasoning from Velopack's behaviour rather than something watched.
