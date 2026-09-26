# Engine repositories

Where a Godot build comes from when it is not one the Godot project published, and how a
workspace asks for one. The first real case is a team's own fork, published as GitHub
releases: `https://github.com/crit-gg/godot-slopworks/releases`.

Read `.claude/godot-engines.md` and the `kitbash-godot` skill first. This plan changes the
identity every part of that skill is keyed on. `tool-distribution.md` is the model for the
repository half, and most of its reasoning carries over unchanged.

## Status

**Built on 25 September 2026**, every decision under Settled as written, apart from the
departures listed under What was built. Measured end to end against the live worked example
on Linux, and nowhere else. See Not tested here.

## The worked example

`crit-gg/godot-slopworks`, read through the API on 25 September 2026. One release.

| | |
|---|---|
| tag | `4.7.2-slopworks-18d5d19`, the base version, the fork's name, a short commit |
| binary | `4.7.2.slopworks.mono.<build>.<commit>`, since the workflow sets `GODOT_VERSION_STATUS=slopworks` |
| editors | `Godot_v4.7.2-slopworks-18d5d19_mono_linux_x86_64.zip`, `..._mono_win64.zip` |
| templates | `Godot_v4.7.2-slopworks-18d5d19_mono_export_templates.tpz` |
| also | four nupkgs, also pushed to `nuget.crit.gg`, which Kitbash ignores |
| checksums | no SHA 512 file. Every asset carries `digest: sha256:<hex>` in the API |
| missing | no macOS build and no standard build, only .NET |
| visibility | public |

**The file names are Godot's own shapes.** Strip `Godot_v<tag>` and the rest is a suffix
`EngineBuild.Shapes` already knows. So the classifier works as it is, given the tag.

**`version.py` says `status = "stable"` and the workflow overrides it.** The channel the
fork was cut from is in neither the tag nor the binary. That is why a repository build has
a channel of its own rather than inheriting one.

**Its templates folder is `4.7.2.slopworks.mono`.** Godot names the folder from
`version.txt` inside the `.tpz`, which is `GODOT_VERSION_FULL_CONFIG` and carries no build
and no commit. Every slopworks 4.7.2 build shares that one folder.

## Settled

### Configuration

- **`godot.repositories` is a list of named entries**, such as a `slopworks` entry of kind
  `github` over `crit-gg/godot-slopworks`. It has a global layer and a team layer, the way
  `tools.repositories` does, so a workspace's committed config can carry the repository its
  engine comes from.
- **`godot.repository` names one entry from that list.** It sits beside `godot.engine` in
  `WorkspaceGodotSettingsSchema` and layers the same way.
- **No `godot.repository` means official builds.** Every workspace that exists today keeps
  exactly the behaviour it has.
- **Adding a repository asks first**, the way `tools/repository?add=` does, since a
  repository is where programs come from.

### Identity

- **An engine is a repository, an `EngineTag`, an optional build and the .NET flag.** The
  word is repository, not flavor, everywhere including code.
- **`EngineChannel.Custom` is a new rank**, and every build a repository publishes has it.
  The repository is a hard filter before any ranking, so an official build and a custom one
  are never compared and where `Custom` sits in the enum changes no answer.
- **Within one repository, builds of the same base version order by release date.** A
  short commit is not orderable.
- **An install is keyed by the repository's kind and address, never its name.** The name
  is a label `godot.repository` looks up. Two names over one address share installs, one
  name over two addresses cannot collide, and renaming an entry orphans nothing.
- **For a workspace, its team list wins over the global list** when both hold an entry of
  the same name, since the team list is the one its pin was written against.

### What a repository release has to look like

- **The tag is `<number>-<name>-<build>`.** Godot's own version number, any name, and the
  build as the last segment after a dash. The worked example fits as it is.
- **A tag that does not fit is skipped and counted.** The engines page says how many
  releases a repository had that it could not read, so whoever publishes the fork notices
  rather than a build silently never appearing.
- **Only assets named `Godot_v<tag>` followed by a known suffix are builds.**
- **A newest pin never moves to a release marked prerelease.** An exact pin may name one,
  so a fork can publish a build for testing without pushing it to everybody. Drafts are
  invisible without a login and are ignored.

### Pins

With `godot.repository` set, `godot.engine` takes two forms.

| Text | Meaning |
|---|---|
| `4.7.2` | a **newest pin**. The newest build of 4.7.2 this repository publishes, kept current |
| `4.7.2+18d5d19` | an **exact pin**. That build and no other, never replaced |

`-mono` still ends either form and means what it means today.

**A pin without a build is a newest pin, and that is the intent.** Nobody writes a newest
pin by accident, since the build is what the exact form adds.

**With a repository and no `godot.engine`, `config/features` is a newest pin.** The fork's
editor writes `4.7` there, so the workspace follows a `4.7` slot inside the repository. That
is the same fallback the official path already has.

**Official pins keep today's behaviour.** `4.7` with no repository opens the best
installed match and offers Install for a missing one. Nothing official ever replaces
itself.

**The `.csproj` is never read.** `Godot.NET.Sdk/<version>` in a project file is not a
requirement source, not a check and not something Kitbash edits. Only the `.kitbash` keys
and `project.godot` decide the engine.

### Slots

- **A newest pin owns one install slot, keyed by its pin text.** `4.7` and `4.7.2` are two
  slots even when both hold the same build.
- **A newer build replaces the slot in place.** The old build is removed, so a person
  following one newest pin never holds more than one copy of it.
- **An exact pin is its own install and never moves.**

### When updates are checked

Every newest pin is checked at each of these:

1. launcher start
2. a workspace becoming the open one
3. Open in Godot, behind the launch dialog before anything starts
4. after a pull brings the workspace branch's changes down from its remote

The fourth is there because a person who has just pulled has asked for the latest of
everything, and that may include a new pin or a new build. It is not the git status beat,
which runs every five seconds and never checks an engine.

A newer build downloads in the background and replaces the slot. A toast reports it.

**Each repository's release list is cached for ten minutes** and every trigger reads
through it except two, which go past it: the engines page's Check for updates, and the
check after a pull, since a person who has just pulled wants the newest answer. When
the network or the rate limit refuses, the stale cache answers, as the official catalogue
already does. The time can come down once `github-login.md` is built.

**A conditional request does not save the allowance.** Measured on 25 September 2026,
unauthenticated against the worked example: three requests carrying the `ETag` each came
back `304` and each took one from `x-ratelimit-remaining`, 58 to 55. The time to live is
the protection.

**A running engine is swapped later.** The download still happens, and the swap waits
until Kitbash sees that engine is no longer running. Open in Godot swaps first, then
starts.

### Export templates

- **Offered for every engine, official and custom.** Godot publishes a `.tpz` per release
  and a repository may too. Never installed without somebody asking.
- **Installed where Godot looks**, `<Godot data dir>/export_templates/<version.txt>`, read
  from inside the archive and never built from the tag. See What Godot itself does with
  directories in `.claude/godot-engines.md`.
- **Once installed for a slot, they follow the slot.** Each later build replaces them too,
  since the first download was the person's choice.
- **Builds of one base version share one templates folder.** Installing one build's
  templates replaces another's, and the page has to say so where two installs share it.
- **Uninstalling removes a templates folder only when Kitbash installed it and no install
  left on the machine uses it.** The confirm says the templates are going and how large
  they are, about 1.9 GB a set. A folder a person installed through Godot's own template
  manager is never touched.

### On disk

Official installs stay at `engines/<id>`, so nothing that exists today moves. A custom
install lives under its repository's address:

```
engines/github/crit-gg/godot-slopworks/4.7.2-slopworks-18d5d19-mono     an exact pin
engines/github/crit-gg/godot-slopworks/newest/4.7.2-mono                a slot
```

GitHub allows only ASCII letters, digits, `-`, `_` and `.` in an owner or repository name,
which every platform accepts in a folder. Anything else is refused rather than escaped.
GitHub ignores case in both, so they are lowercased, which keeps two spellings from
becoming two folders on a filesystem that ignores case and one that does not.

### Repository kinds

The two operations from `tool-distribution.md`, and nothing more: list what a repository
offers, fetch a named file for one release.

| Kind | Lists | Fetches | Checksum |
|---|---|---|---|
| `godot` | `versions.json` | the `godot-builds` manifest, then the file | SHA 512 |
| `github` | the releases API, one call for every release and its assets | the release asset | the asset's `digest`, SHA 256 |

- **The official feed becomes the first kind as part of this work.** `EngineCatalogue`
  turns into one catalogue over any number of repositories, with `godot` as the one that
  needs no entry. Official and custom builds go down one path.
- **`github` reuses what `GitHubToolRepository` already does**, since the release list
  and asset fetch are the same calls.
- **`EngineRecord` gains the repository and the checksum algorithm.** Today its checksum
  is assumed to be SHA 512.

### Out of scope

- NuGet and the nupkgs. The project's own `nuget.config` is the project's business.
- Private repositories and the unauthenticated allowance of 60 requests an hour. Both wait
  for `github-login.md`.
- A platform a repository does not publish for. The card says there is no build for this
  machine, which on macOS is the worked example today.

## What was built

**Core**, all under `Kitbash.Core/Godot` unless named.

- `EngineRepositoryAddress` is the kind and address, lower cased. `EngineRepositorySource` is
  one named entry and `IEngineRepositoryList` reads both lists, the workspace's first.
- `EngineId` gained `Repository` and `Build`, and `EngineChannel` gained `Custom`.
  `EngineVersionPattern.TryParseForRepository` reads a repository pin, `+build` included, and
  the official parse refuses a build.
- `IEngineRepository` is the two operations. `EngineCatalogue` is the official one and
  `GitHubEngineRepository` the other, made by `IEngineRepositories`. A release carries its
  published name, its build, whether it is a prerelease and when it was published.
- `EngineManifest` carries which hash it holds and the export templates. `EngineRecord`
  carries the slot, the hash kind, the release and the templates folder, in the record file.
- `EngineLayout` places every install, `EngineDownloads` fetches and verifies with either
  hash, and `EngineInstaller` installs, stages and places.
- `IEngineUpdater` finds, installs and updates. `IEngineTemplates` installs, measures and
  removes templates. `IRunningPrograms` in `Platform` reads the .NET process list, which took
  225 ms over 646 processes here, and is one implementation on every OS.
- `GodotLaunchStage.Updating` is the step the launch dialog shows while a slot moves.

**The launcher.**

- The strip names the repository in the pill where a channel goes, says when a workspace
  names a repository no list holds, and installs a repository pin into its slot.
- `LauncherViewModel` runs the four checks and says what they did in toasts: updated,
  waiting for Godot to close, templates not updated, or could not update. A repository that
  cannot be reached says nothing, since nobody asked.
- The engines page lists every repository's releases beside the official ones, under all
  and a new custom choice, with the repository in the pill and a PRERELEASE badge. The
  channel choice is bound both ways now, so installing a repository build from the strip
  moves the filter to custom and the card it draws on is on screen. A note
  says when a repository could not be read or published tags it skipped. An installed row
  says which pin a slot follows and whether it has templates, and its menu adds or removes
  them. Uninstalling takes templates nothing else reads, and the dialog says so with a size.
- The Engine repositories settings page edits the global list, and
  `kitbash://engines/repository?add=<address>&name=<name>` adds to it after a dialog.

**Departures from the plan.**

- **The New Workspace dialog offers no repository build.** It writes `godot.engine` alone
  and cannot write a repository.
- **Set as default is hidden on a repository build.** Its name changes each time its slot
  moves, and `godot.engines.default` only takes an official name.
- **Kitbash reads no repository without a record**, as the `kitbash-godot` skill says.

## Not tested here

**Measured on Linux against the live worked example**, in a temporary home: the list read,
the Linux .NET build picked, downloaded, verified against its SHA 256 digest, unpacked into
`newest/4.7.2-mono` and probed as `4.7.2.slopworks.mono.custom_build.18d5d19a8`, the
workspace resolved to it as matched, a check found it current, and the templates landed as
7 files and 354 MB in `export_templates/4.7.2.slopworks.mono`. The replace, the wait for a
running editor, the prerelease rule and a bad digest are covered by tests over a faked
network, with real files.

What was not:

- **Windows.** Whether renaming a slot folder fails while its editor runs, which the swap
  relies on as a second guard after `IRunningPrograms`. Where the templates go,
  `%APPDATA%\Godot\export_templates`, is read from Godot's source and not run.
- **macOS.** The worked example publishes no macOS build, so no repository install has run
  there. The templates path is read from Godot's source.
- **A running editor.** `IRunningPrograms` was measured against this process, not against a
  Godot that was open, since nothing here opens a window.
- **The strip and the settings page were not drawn.** The engines page was, both tabs with a
  slot and a repository's releases, in `EnginesPageRepositoryTests`. That is what showed the
  channel choice was never bound, so a repository install now moves it to custom and the
  row says so.
- **Importing a slopworks build by hand still fails**, since probing cannot say which
  repository it came from.
