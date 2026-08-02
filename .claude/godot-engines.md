# Godot engine discovery, download and installation

Research for the engines page. What the existing open source Godot version managers do,
where the engine builds and their release notes actually live, and what installing an
engine costs on each platform we support.

Nothing here is built yet. This is the reference to work from when it is.

**Scope: Godot 4 and later.** Godot 3 and below are not offered and not installed. Their
naming carries per version overrides for most of the 1.x and 2.x range and a different
spelling for Linux and macOS, and none of it is worth carrying. Where this document
describes Godot 3, it is background rather than a requirement, and it is marked.

**The whole of Godot 4 was swept on 2 August 2026**, all 183 releases from 4.0-alpha1 to
4.8-dev2, by fetching every release manifest and reading inside twenty of the archives.
That sweep is the section called The landscape, and it replaced four guesses that were
each wrong in a different way. Read it before writing anything that names a file.

Everything marked **measured** was run on this machine on 1 August 2026, against
Godot 4.7.1 and .NET 10.0.302, on Linux. Everything marked **read** came from source.
Windows behaviour is read only, since this machine is Linux, and is called out where
it matters.

## The tools surveyed

| Tool | Language | Shape | Licence | What it is worth reading for |
|---|---|---|---|---|
| [gdvm](https://github.com/adalinesimonian/gdvm) | Rust | CLI, shim on PATH | GPL 3 | its own build registry, download resume, per project pins |
| [GodotEnv](https://github.com/chickensoft-games/GodotEnv) | C# on .NET | CLI | MIT | closest to our stack, asset naming per platform, symlink and PATH work |
| [Godots](https://github.com/MakovWait/godots) | GDScript | GUI, written in Godot | MIT | the GUI flows, importing an engine already on disk |
| [Godot Launcher](https://github.com/godotlauncher/launcher) | TypeScript, Electron | GUI | MIT | per project editor settings through self contained mode |

The user of this machine already has gdvm installed, so several measurements below come
from a real install tree rather than a synthetic one.

Also seen and not read in depth: `eumario/godot-manager`, `noidexe/godot-version-manager`,
`Kyrio/godot-point`, `godl`. They cover the same ground with less to teach.

## Where the builds live

**One repository serves every download.** `godotengine/godot-builds` holds pre releases
and dev snapshots, and it also carries a full copy of every stable release. The main
`godotengine/godot` repository carries stable releases only.

**Measured:** the same file resolves from both repositories.

```
https://github.com/godotengine/godot-builds/releases/download/4.4.1-stable/Godot_v4.4.1-stable_linux.x86_64.zip   200
https://github.com/godotengine/godot/releases/download/4.4.1-stable/Godot_v4.4.1-stable_linux.x86_64.zip          200
https://github.com/godotengine/godot-builds/releases/download/4.8-dev2/Godot_v4.8-dev2_linux.x86_64.zip           200
```

So one host and one URL shape covers stable and pre release alike. GodotEnv relies on
this and only ever builds `godot-builds` URLs. Do the same.

The release tag is `<version>-<release>`, such as `4.7.1-stable`, `4.8-dev2`,
`4.7-rc3`. That tag is also the folder name in the download URL.

## Three ways to discover versions, and which to use

### 1. `https://godotengine.org/versions.json` (recommended)

A static file the website publishes. No authentication, no rate limit, small.

**Measured:** 200, 48722 bytes, 74 entries, newest first, from 4.8 down to 1.0.

```json
[
  { "name": "4.8", "releases": [
      { "name": "dev2", "release_date": "21 July 2026",
        "release_notes": "https://godotengine.org/article/dev-snapshot-godot-4-8-dev-2/" },
      { "name": "dev1", "release_date": "6 July 2026", "release_notes": "..." }
  ]},
  { "name": "4.7.1", "releases": [ { "name": "stable", ... }, { "name": "rc2", ... } ]}
]
```

Every entry has exactly `name` and `releases`. Every release has exactly `name`,
`release_date` and `release_notes`. Dates are human strings such as "21 July 2026", not
ISO, so they need parsing with an invariant culture and a day month year format.

**Corrected 2 August 2026.** This said no release is missing its notes URL, and one is.
`3.2-alpha0-unofficial` carries an empty string. It is the same entry that fails the tag
grammar, so a reader that drops what it cannot parse never reaches the empty value. Do
not rely on that: read the field as optional.

This gives the version list and the release notes link in one request, which is the
whole engines page apart from the file sizes.

Its source is `_data/versions.yml` in `godotengine/godot-website`, which carries two
extra fields the JSON drops: `flavor`, the current release of that line, and `featured`,
which drives the download page's highlight. Godots parses the YAML with regular
expressions. Prefer the JSON. The flavor is recoverable, since it is the first entry of
`releases`.

### 2. The GitHub releases API

`https://api.github.com/repos/godotengine/godot-builds/releases` gives release bodies,
asset lists, sizes, download counts and per asset digests.

**Measured:** unauthenticated requests are limited to 60 per hour per address.
Response headers say so plainly.

```
x-ratelimit-limit: 60
x-ratelimit-remaining: 57
x-ratelimit-resource: core
```

Godot Launcher pages this API and stores the newest `published_at` it has seen so later
refreshes ask only for what is newer. gdvm used to do the same and gave up: its changelog
records moving off the API to a static registry so that update checks "no longer require
GitHub API access or are affected by rate limits", and removing the `github.token` setting
that existed to work around the limit.

**Take the lesson rather than the API.** A desktop app that hits a 60 per hour shared
limit will hit it, because the limit counts every process behind one address, including
the person's browser and their other tools. Use `versions.json` for the list, and go to
the API only for a single release when there is something only it can answer.

An asset entry, for when it is needed:

```json
{ "name": "Godot_v4.7.1-stable_linux.x86_64.zip", "size": 76056717,
  "digest": "sha256:0230d4...", "browser_download_url": "https://github.com/..." }
```

The `digest` field is newer than most of the tools surveyed and none of them use it.

### 3. A static registry of your own

gdvm now serves `https://registry.gdvm.io/v2/index.json` plus one file per release.

```json
{ "schema": 2,
  "releases": [ { "version": "4.8-dev2",
      "variants": { "default": ["linux-x86_64", "windows-x86_64", ...],
                    "csharp":  ["linux-x86_64", ...] },
      "path": "releases/4.8-dev2.json" } ] }
```

And per release, keyed by variant then platform:

```json
{ "schema": 2, "updated_at": "2026-07-14T18:56:48.886Z", "version": "4.7.1-stable",
  "variants": { "default": { "linux-x86_64": {
      "sha512": "de64ef...",
      "urls": ["https://github.com/godotengine/godot-builds/releases/download/..."] } } } }
```

Worth knowing about because it is the shape a custom registry takes, and because gdvm
lets a team point at one to serve their own engine builds. We do not need to run one.
It is also a reminder that the client should not care where a build came from, only
that it has a URL, a checksum and a platform key.

## The official checksums

`https://raw.githubusercontent.com/godotengine/godot-builds/main/releases/godot-<tag>.json`

**Measured** for `godot-4.4.1-stable.json`:

```json
{ "name": "4.4.1", "version": "4.4.1", "status": "stable",
  "release_date": 1742980077, "git_reference": "4.4.1-stable",
  "files": [ { "filename": "Godot_v4.4.1-stable_linux.x86_64.zip",
               "checksum": "f67882a7..." } ] }
```

27 files for that release. The checksum is **SHA 512**, lower case hex. This is what
GodotEnv verifies against, and it is the only checksum published by the Godot project
itself rather than by GitHub. `release_date` here is a Unix timestamp, unlike the
website's prose dates.

GodotEnv's messages record that no checksums exist below 3.2.2-beta1. Anything older
cannot be verified this way.

The same directory listing, `https://api.github.com/repos/godotengine/godot-builds/contents/releases`,
is what GodotEnv uses to list versions. It costs a GitHub API call and returns file names
such as `godot-4.7.1-stable.json`. `versions.json` is better for that job.

## Version strings

Three spellings of the same thing, and mixing them up is where these tools spend their
bug budget.

| Style | Example | Where it appears |
|---|---|---|
| release tag | `4.7.1-stable`, `4.8-dev2`, `2.0.4.1-stable` | download URLs, checksum files, `versions.json` |
| build string | `4.7.1.stable.mono.official.a13da4feb` | what the binary prints, export template folder names |
| SDK style | `4.7.1`, `4.7.1-beta.3` | `Godot.NET.Sdk` in `global.json` |

The grammar of a release tag, from GodotEnv's regular expression, which is the tightest
statement of it anywhere in these projects:

```
^(\d+)\.(\d+)(\.[1-9]\d*)?-(stable|([a-z]+)(\d+))$
```

Read out: major and minor are always present. **Patch is omitted when it is zero**, so
`4.7` and not `4.7.0`. Every Godot 4 release carries a label. `stable` never carries a
number and everything else always does. Godot 1 and 2 also produced four component
versions such as `2.0.4.1`, which this expression rejects and gdvm accommodates with a
fourth number it calls the subpatch.

**The .NET build is a separate download of the same version, not a separate version.**
It is called mono in every file name and dotnet or csharp in the tools. Model it as a
flag on a version rather than as part of the version, which is what GodotEnv does with
`SpecificDotnetStatusGodotVersion`. Two installs of `4.7.1-stable` can coexist, one with
and one without.

**Ordering.** By major, minor, patch, then by label rank, then by label number. The rank
is `dev` < `alpha` < `beta` < `rc` < `stable`. Note gdvm's table omits alpha and so ranks
it below dev, which only matters for Godot 3 era listings. Do not repeat it.

## What the files are called, as first believed

**Superseded by The landscape below, which measured it.** Kept because it is what
`_data/download_configs.yml` in `godotengine/godot-website` states, and because three of
the claims under it turned out to be wrong. Filenames are `Godot_v<tag>` plus the suffix.

Godot 4:

| Platform | Standard | .NET |
|---|---|---|
| Linux x86_64 | `_linux.x86_64.zip` | `_mono_linux_x86_64.zip` |
| Linux x86_32 | `_linux.x86_32.zip` | `_mono_linux_x86_32.zip` |
| Linux arm64 | `_linux.arm64.zip` | `_mono_linux_arm64.zip` |
| Windows x86_64 | `_win64.exe.zip` | `_mono_win64.zip` |
| Windows x86_32 | `_win32.exe.zip` | `_mono_win32.zip` |
| Windows arm64 | `_windows_arm64.exe.zip` | `_mono_windows_arm64.zip` |
| macOS | `_macos.universal.zip` | `_mono_macos.universal.zip` |
| export templates | `_export_templates.tpz` | `_mono_export_templates.tpz` |

Godot 3 differs: Linux is `_x11.64.zip` and macOS is `_osx.universal.zip`. Windows arm64
first appears in 4.3. The YAML also carries a list of overrides for versions where the
defaults do not hold, which is most of the 1.x and 2.x range.

**The two naming traps, both real and both silent.**

First, the Linux .NET download separates its platform words with underscores while the
executable inside separates them with a dot. **Measured** from the archive on disk:

```
Godot_v4.7.1-stable_mono_linux_x86_64.zip
  Godot_v4.7.1-stable_mono_linux_x86_64/
  Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
  Godot_v4.7.1-stable_mono_linux_x86_64/GodotSharp/...
```

Second, **the .NET archive wraps its contents in a folder and the standard one does not**.
**Measured** on the standard Linux archive: one entry, no folder.

```
Archive:  Godot_v4.7.1-stable_linux.x86_64.zip
-rwxr-xr-x  3.0 unx 144583504 defX  Godot_v4.7.1-stable_linux.x86_64
1 file
```

So the path to the executable after extraction is not a fixed shape. GodotEnv computes it
per platform and per .NET status and still leaves a message in the code apologising for
when it is wrong. gdvm instead strips the common prefix from the archive at extraction
time, so every install directory has the same shape whatever went in. **Measured** on
this machine's gdvm tree, where the wrapper folder is gone:

```
~/.gdvm/installs/4.7.1-stable-csharp/Godot_v4.7.1-stable_mono_linux.x86_64
~/.gdvm/installs/4.7.1-stable-csharp/GodotSharp/
~/.gdvm/installs/4.5.2-stable/Godot_v4.5.2-stable_linux.x86_64
```

Stripping is the better answer. It turns a naming rule that has changed several times
into one that does not have to be known at all.

## The landscape

The whole of Godot 4 as it was actually published, rather than as a table says it should
be. **Measured 2 August 2026**: every one of the 183 releases in the version feed, from
`4.0-alpha1` on 24 January 2022 to `4.8-dev2` on 21 July 2026, by fetching each release
manifest from `godot-builds`. All 183 came back in 2.6 seconds and none failed.

Across those releases there are **38 distinct asset shapes** and **19 of them are desktop
editors**. The rest is export templates, the Android editor in five flavours, the web
editor, the Android libraries, native debug symbols and the source tarball.

### The nineteen desktop editors, and when each one existed

`Godot_v<tag>` plus the suffix. First and last are the releases each shape appears in.

| Suffix | Platform | Processor | .NET | First | Last |
|---|---|---|---|---|---|
| `_linux.32.zip` | Linux | x86_32 | | 4.0-alpha1 | 4.0-alpha14 |
| `_linux.64.zip` | Linux | x86_64 | | 4.0-alpha1 | 4.0-alpha14 |
| `_linux.x86_32.zip` | Linux | x86_32 | | 4.0-alpha15 | current |
| `_linux.x86_64.zip` | Linux | x86_64 | | 4.0-alpha15 | current |
| `_linux.arm32.zip` | Linux | arm32 | | 4.2-beta5 | current |
| `_linux.arm64.zip` | Linux | arm64 | | 4.2-beta5 | current |
| `_mono_linux_x86_32.zip` | Linux | x86_32 | yes | 4.0-alpha17 | current |
| `_mono_linux_x86_64.zip` | Linux | x86_64 | yes | 4.0-alpha17 | current |
| `_mono_linux_arm32.zip` | Linux | arm32 | yes | 4.2-beta5 | current |
| `_mono_linux_arm64.zip` | Linux | arm64 | yes | 4.2-beta5 | current |
| `_win32.exe.zip` | Windows | x86_32 | | 4.0-alpha1 | current |
| `_win64.exe.zip` | Windows | x86_64 | | 4.0-alpha1 | current |
| `_windows_arm64.exe.zip` | Windows | arm64 | | 4.3-rc1 | current |
| `_mono_win32.zip` | Windows | x86_32 | yes | 4.0-alpha17 | current |
| `_mono_win64.zip` | Windows | x86_64 | yes | 4.0-alpha17 | current |
| `_mono_windows_arm64.zip` | Windows | arm64 | yes | 4.3-rc1 | current |
| `_osx.universal.zip` | macOS | universal | | 4.0-alpha1 | 4.0-alpha12 |
| `_macos.universal.zip` | macOS | universal | | 4.0-alpha13 | current |
| `_mono_macos.universal.zip` | macOS | universal | yes | 4.0-alpha17 | current |

Everything else a manifest holds, and none of it is an editor Workbench can install:
`_export_templates.tpz`, `_mono_export_templates.tpz`, `_web_editor.zip`,
`_android_editor.apk`, `_android_editor.aab`, `_android_editor_meta.apk`,
`_android_editor_horizonos.apk`, `_android_editor_picoos.apk`,
`_android_debug.perfetto.apk`, `_android_release.perfetto.apk`,
`_android_source.perfetto.zip`, the `godot-lib.*.aar` libraries,
`Godot_native_debug_symbols.*` and `godot-<tag>.tar.xz`.

### What that table kills

Four things were believed before the sweep and all four are wrong.

**Godot 4 has three Linux spellings, not one.** The first fourteen alphas used
`_linux.64.zip`, and it became `_linux.x86_64.zip` at 4.0-alpha15. The `x11` spelling is
Godot 3 only, which was the one part that held.

**Godot 4 has two macOS spellings.** `_osx.universal.zip` up to 4.0-alpha12, then
`_macos.universal.zip`. The osx spelling is not a Godot 3 marker.

**There was no .NET build at all until 4.0-alpha17.** Sixteen releases carry no mono
download of any kind, so .NET is not a flag that always has an answer.

**Every boundary is at pre release granularity, not at a minor version.** Linux on ARM
starts at **4.2-beta5** and Windows on arm64 at **4.3-rc1**. A rule reading "4.3 and
later" is wrong for `4.3-dev1` through `4.3-beta3`, and a rule reading the minor number
alone is wrong in a way that only shows on an old pre release.

### So do not derive a file name. Ask the manifest.

`https://raw.githubusercontent.com/godotengine/godot-builds/main/releases/godot-<tag>.json`
lists exactly what a release holds, with a SHA 512 for each file. Measured: 8254 bytes and
0.11 seconds for `4.7.1-stable`, from a CDN with no rate limit.

That one request answers every question above and cannot go stale. It also carries the
checksum, which has to be fetched before installing anyway, so on the common path it costs
nothing extra. **The naming table becomes a classifier and never a generator**: take a
published name, strip `Godot_v<tag>`, look the suffix up in the nineteen, and drop
whatever does not match. No version logic survives.

This is the posture git already has in this app. Where git keeps a repository is asked,
never guessed. What a release contains is asked, never guessed.

GodotEnv is the counter example and is worth reading as one. It derives, and:

- `Linux.GetPlatformNameAndArchitecture` returns `x86_64` for every Godot 4 version, so it
  cannot install a Linux arm64 or arm32 build at all.
- `Windows.GetProcessorArchitecture` tests
  `version.Number.Major < 4 || version.Number.Minor < 3`, reading the minor without first
  checking the major. Right for 4.3 through 4.9 and wrong for any later major, where 5.0
  takes the `Minor < 3` branch and asks for a file name that does not exist.
- `MacOS.GetInstallerNameSuffix` tests `Minor > 3 && Patch > 2` for its universal
  boundary, which fails on a zero patch.

None of those are careless. They are what deriving costs.

### What is inside the archives

**Measured** across twenty archives spanning 4.0-alpha1 to 4.8-dev2, by reading each zip's
central directory over an HTTP range request. Between 0.2 and 31 KB moved per archive
rather than the 1.7 GB the files come to. The technique is worth keeping: a zip's index is
at its end and `Range` reaches it, so an archive can be inspected without being fetched.

| Archive | Top level | Entries | The editor inside |
|---|---|---|---|
| Linux standard | the binary itself | 1 | the one entry, mode 0755 |
| Linux .NET | one folder | 41 to 74 | in the folder, beside `GodotSharp/` |
| Windows standard | **two files** | 2 | the `.exe` that is not the console one |
| Windows .NET | one folder | 47 to 82 | in the folder |
| macOS standard | `Godot.app` | 79 to 80 | `Godot.app/Contents/MacOS/Godot` |
| macOS .NET | `Godot_mono.app` | 119 to 153 | `Godot_mono.app/Contents/MacOS/Godot` |

**The rule that the .NET archive wraps its contents in a folder and the standard one does
not is wrong, and this document said it.** It holds for Linux and for nothing else.
Windows ships two loose files and both macOS archives are an application bundle.

**So the prefix strip needs a guard.** gdvm strips the common prefix so every install
directory comes out the same shape, which is still the right idea, but applied blindly it
lifts `Godot.app/Contents/...` to the root and destroys the bundle. The rule: strip a
single top level entry only when it is a directory whose name does not end in `.app`.
Windows standard has two top level entries, so there is no common prefix and nothing is
stripped, which is right but only by accident, so write it deliberately.

**Windows ships a console executable beside the editor**, which is why its standard archive
holds two files. Measured: 4.0-alpha1 spelled it `_console.cmd` and every release from
4.0-stable on spells it `_console.exe`. Launch the plain one. "The `.exe` that is not
`_console.*`" is one rule covering both.

**The executable bit is carried by the archive on Unix.** Measured across every Linux
archive in the sweep: the editor binary is 0755. A .NET archive also carries 0744 on many
files under `GodotSharp/Tools`, which is harmless. Windows entries are 0644 or 0664, which
means nothing on Windows and would matter only if a Windows archive were unpacked on Unix.

**Do not work the executable out from a file name.** After extracting, the candidates are
few and obvious: on Unix a file carrying the executable bit that is not under
`GodotSharp/`, and on Windows an `.exe` that is not the console one. Confirm the choice
with `--version`, which has to run anyway to identify the install. That takes the last
naming rule out of the installer.

## Release notes, and the four places they live

For a given release, all four are reachable and they say different things.

1. **The article on the website.** `https://godotengine.org/article/<slug>/`. The URL
   comes from `versions.json` and needs no derivation. This is the human release notes.
2. **The GitHub release body.** Short, and mostly a set of links. **Measured** for
   `4.7.1-stable` it names the release kind, links the article, the interactive changelog
   and the curated changelog. **Measured** for `4.8-dev2` it also names the commit it was
   built from and the `GODOT_VERSION_STATUS` value needed to reproduce it.
3. **The interactive changelog.**
   `https://raw.githubusercontent.com/godotengine/godot-interactive-changelog/master/data/godotengine.godot.<version>.json`.
   Commit and pull request level detail, with `release_logs` split per pre release.
   **Measured:** 250 KB for the 4.7.1 patch release and 4.7 MB for the 4.7 feature
   release. Too heavy to fetch speculatively. Fetch when someone asks for it.
4. **The curated changelog.** `CHANGELOG.md` in the `godotengine/godot` repository at
   that tag. Only exists for stable releases.

**The blog feed is the fifth source and it is the one to use for "what is new".**
`https://godotengine.org/rss.xml`. **Measured:** 15 items, each with the full article
HTML in `description`, plus `summary`, `image`, `pubDate` and a `category` from
`Events`, `News`, `Pre-release` and `Release`. That is a complete release note without a
second request, but only for the most recent fifteen posts. For anything older, take the
URL from `versions.json`.

## Downloading

What the surveyed tools agree on, and what only one of them gets right.

**Verify the checksum.** GodotEnv computes SHA 512 over the archive and compares against
the published `godot-<tag>.json`. It refuses to install on a mismatch and makes the
override an explicit `--unsafe-skip-checksum-verification` flag. A missing checksum is
also a refusal rather than a shrug. Copy this posture.

**Cache the archive and mark it complete separately.** GodotEnv writes a `.done` marker
file beside the download and treats the archive as reusable only when both exist. A half
written archive from a killed process therefore never looks like a cached one. gdvm keeps
the same idea and adds resume: it stores the server's `ETag` or `Last-Modified`, sends
`Range` with `If-Range`, and falls back to restarting when the server does not honour it
or the validator no longer matches. **Measured** on this machine, gdvm's cache holds the
four archives it has ever downloaded, 70 to 106 MB each, so caching them is not free.

**Guard the extraction.** gdvm rejects any entry whose path escapes the target after the
prefix strip, and rejects any entry that decompresses past its declared size, which is
the zip bomb guard. Both are cheap and both belong in ours.

Sizes to plan the UI around, all measured: an editor archive is 70 to 106 MB, and the
extracted binary is 137 to 145 MB.

## Extracting, and the executable bit

Godots shells out to `unzip` on Linux and `Expand-Archive` on Windows. GodotEnv uses
`System.IO.Compression` on Windows and shells out to `unzip` everywhere else. The reason
given in both is that the managed extractor loses the executable bit.

**That is no longer true, and we should not carry the workaround.**

**Measured** on .NET 10.0.302 on Linux, `ZipFile.ExtractToDirectory`:

| Archive | Entry mode in archive | Mode after extraction |
|---|---|---|
| `zip` built, script marked 0755 | `-rwxr-xr-x` | `0755` |
| the real `Godot_v4.7.1-stable_linux.x86_64.zip` | `-rwxr-xr-x` | `0755` |
| entry written with MS DOS attributes | `-rw-a--` (fat) | `0644` |

So the mode comes from the archive and .NET honours it. The official Linux and .NET
archives both carry `0755` on the editor binary, so extraction alone is enough.

The third row is why a guard is still wanted. An archive built on Windows carries no
Unix mode and its entries land without the executable bit. So extract with
`System.IO.Compression`, then, on Unix, set the executable bit on the engine binary if
it is not already set. That costs one syscall and removes any dependency on `unzip`
being installed, which matters for us: our rules forbid assuming a program is present,
and Godots' Linux path fails silently on a distribution or a container without it.

## Where an engine gets installed, and how it is launched

The four tools disagree completely, which is itself the finding.

| Tool | Install root | Naming | Launch |
|---|---|---|---|
| gdvm | `~/.gdvm/installs` | `4.7.1-stable-csharp` | `~/.gdvm/bin/godot`, a shim binary that resolves the version at run time |
| GodotEnv | app data, `godot/versions` | `godot_dotnet_4_7_1_stable` | a symlink at `godot/bin/godot`, plus a `GODOT` variable and a PATH entry |
| Godots | `user://versions` under its own data dir | as extracted | runs the binary directly with per engine custom commands |
| Godot Launcher | `~/Godot/Editors`, config in `~/.godotlauncher` | per release | a per project link under `<installs>/.editor_config/<project>` |

**Measured** on this machine, gdvm's layout:

```
~/.gdvm/bin/gdvm          the tool
~/.gdvm/bin/godot         a 513 KB shim, not a symlink
~/.gdvm/cache/*.zip       downloaded archives
~/.gdvm/cache.json        the registry index cache
~/.gdvm/installs/4.7.1-stable-csharp/
```

**Godot Launcher puts config and installs in invented home directories** and ignores
XDG and `%APPDATA%` entirely. Do not copy that. Our `IUserDirectories` already answers
this question correctly for both platforms, and an engine install is data rather than
configuration, so it belongs under the data directory.

### Symlinks, shortcuts and PATH

Only relevant if we ever want an engine reachable from outside Workbench. Reading it is
worthwhile anyway, because it is a catalogue of what each platform charges.

**Windows symlinks need a privilege.** GodotEnv creates every symlink by running
`cmd.exe /c mklink` elevated, and deletes them elevated too. Godot Launcher instead
exposes a `windows_enable_symlinks` preference and copies the files when it is off.
Copying a 145 MB binary per project is the cost of that fallback. `File.CreateSymbolicLink`
in .NET works without elevation only when Developer Mode is on, so a tool that needs a
link on Windows needs a fallback whatever it chooses.

**Linux has a three step fallback worth stealing.** Godot Launcher tries a hard link,
then a symlink, then a copy. A hard link is free and shares the bytes, but it fails
across filesystems, which is exactly what happens when the install root and the project
are on different mounts.

**The desktop entry.** GodotEnv writes `~/.local/share/applications/Godot.desktop` with
`MimeType=application/x-godot-project`, downloads an icon into
`~/.local/share/icons/godot.png`, and copies the fields from the entry Godot itself ships
at `misc/dist/linux/org.godotengine.Godot.desktop`. It hardcodes those two directories
rather than reading `XDG_DATA_HOME`, which is wrong under Flatpak and Snap. On Windows
it drives PowerShell and `WScript.Shell` to write a `.lnk` into the Start Menu.

**PATH.** On Windows GodotEnv sets the user scoped `GODOT` and `PATH` variables through
`Environment.SetEnvironmentVariable`. On Unix it writes `~/.config/godotenv/env` and
appends a source line to `.profile`, `.bashrc` and `.zshenv`, and its own message admits
this misses fish. Editing a person's shell files is intrusive and we should not do it
without being asked.

## What Godot itself does with directories

Read from the Godot 4.7.1 source at `/home/jason/Projects/godot/godot-src-471`. This is
the part that decides whether two installed engines interfere with each other.

**The directory name is `godot` everywhere except Windows, where it is `Godot`.**
`OS::get_godot_dir_name` lowercases the short name, and `OS_Windows` overrides it to
capitalise. Same folding rule our own `IUserDirectories` applies.

| | Linux | Windows |
|---|---|---|
| data | `$XDG_DATA_HOME/godot` or `~/.local/share/godot` | `%APPDATA%\Godot` |
| config | `$XDG_CONFIG_HOME/godot` or `~/.config/godot` | `%APPDATA%\Godot` |
| cache | `$XDG_CACHE_HOME/godot` or `~/.cache/godot` | `%LOCALAPPDATA%\Godot` |

Godot ignores an XDG variable holding a relative path and warns, which is the same rule
the specification states and the one we already implement.

**Editor settings are per minor version since 4.3.** `editor_settings-<major>.<minor>.tres`
in the config directory, and `editor_settings-4.tres` before 4.3. On a version bump Godot
walks minor versions downward to find an existing file to migrate from.

**Measured** on this machine, which is exactly what the source predicts:

```
~/.config/godot/editor_settings-4.5.tres
~/.config/godot/editor_settings-4.6.tres
~/.config/godot/editor_settings-4.7.tres
```

So 4.7.0 and 4.7.1 share one settings file, and 4.6 and 4.7 do not. Two installs of the
same minor version, .NET and standard, share everything.

**Self contained mode is the escape hatch, and it is one empty file.** If `_sc_` or
`._sc_` sits next to the executable, Godot puts data, config and cache in
`<exe dir>/editor_data` instead of the user directories. Read from
`editor/file_system/editor_paths.cpp`.

```cpp
// Self-contained mode if a `._sc_` or `_sc_` file is present in executable dir.
if (self_contained) {
    data_dir = exe_path.path_join("editor_data");
    config_dir = data_dir;
    cache_dir = data_dir.path_join("cache");
}
```

**This is how Godot Launcher gives every project its own editor settings.** For each
project it makes a directory `<installs>/.editor_config/<project name>`, writes an empty
`._sc_` into it, and links the engine binary in beside it, plus `GodotSharp` and the
console executable where those apply. That directory, and not the install directory, is
what gets launched. Godot then writes that project's settings, layouts and shader cache
into `editor_data` there. The project folder itself stays clean, and the engine bytes are
shared rather than copied, as long as the link succeeds.

Godots reads the same marker to report whether an engine is self contained, but its
settings path uses the pre 4.3 `editor_settings-4.tres` name, which is now wrong.

If we ever want per workspace editor settings, that is the mechanism, and it costs a
directory, a link and an empty file.

**Export templates are large and are keyed by build string.** They live in
`<data dir>/export_templates/<version.txt contents>`, where `version.txt` is a file inside
the `.tpz` archive. Read from `editor/export/export_template_manager.cpp`, which reads
that file first, uses it as the folder name, and flattens the archive's `templates/`
directory into it.

**Measured** on this machine:

```
~/.local/share/godot/export_templates/4.7.1.stable.mono/version.txt   ->  4.7.1.stable.mono
~/.local/share/godot/export_templates/4.7.1.stable.mono               ->  1.9 GB
```

1.9 GB per version per flavour. Five sets are installed here. If the engines page ever
shows disk usage, this is the number that matters, not the 145 MB editor.

The folder name is `GODOT_VERSION_FULL_CONFIG`, which is
`<number>.<status>[.<module_config>]`, so `4.7.1.stable` or `4.7.1.stable.mono`. Do not
build it from the release tag by string surgery. Ask the binary instead, see below.

## Identifying an engine already on the machine

**Ask the binary.** `--version` prints the full build string and exits.

**Measured:**

```
$ Godot_v4.7.1-stable_mono_linux.x86_64 --version
4.7.1.stable.mono.official.a13da4feb

$ Godot_v4.5.2-stable_linux.x86_64 --version
4.5.2.stable.official.6ce3de25a
```

**Measured:** 20 ms wall clock. Cheap, and still not something to do on the UI thread.

The string is `<number>.<status>[.<module_config>].<build>.<commit>`. Every fact the
engines page needs is in it: the version, the release kind, whether it is the .NET build,
whether it is an official build or someone's own, and the commit. Dropping the last two
components gives the export template folder name exactly.

This beats parsing the file name, which is what Godots does with a scoring function that
compares two version hints and returns a similarity out of 100. A file name can be
renamed. The build string cannot.

**Finding candidates.** gdvm scans an install directory for names starting `Godot_v` or
ending `.x86_64`, `.x86_32` or `.arm64` on Linux, and on Windows prefers an `.exe` that
is not `_console.exe`. Godots takes a different route for importing an engine a person
already has: it lists the directory, filters to plausible executables, and asks the person
to confirm which one. For a GUI that is the better answer, because the guess is visible
and correctable.

**Windows ships two executables per build.** `Godot_v<tag>_win64.exe` and
`Godot_v<tag>_win64_console.exe`. The plain one detaches from the console and the console
one keeps it. Launch the plain one, and offer the console one only where someone wants
engine output. Godot Launcher links both into its per project directory.

## Pinning a version to a project

Four mechanisms exist in the wild. The first is the only one Godot itself defines.

**`project.godot` says which version wrote it.** gdvm reads
`[application] config/features=PackedStringArray("4.4", ...)` for the version, treats
`config_version=4` as meaning Godot 3, and takes a `[dotnet]` section as meaning the .NET
build is wanted. This needs no extra file and works on any project, which makes it the
right default for us. We already read `project.godot` by line for the project name, so
the reader exists.

The rest are each tool's own file, and none of them interoperate:

- gdvm writes `gdvm.toml` with a `[godot] version` key, and older versions used `.gdvmrc`
- GodotEnv writes `.godotrc`, a single line such as `4.7.1-stable no-dotnet`
- GodotEnv also reads and writes `msbuild-sdks` `Godot.NET.Sdk` in `global.json`, and
  reads any `.csproj` in the tree, both of which are real .NET project files rather than
  something invented

GodotEnv walks ancestor directories collecting all of them and takes the first that
parses, preferring `global.json`, then `.csproj`, then `.godotrc`.

For Workbench the answer is our own settings. A `workspace.engine` key in the team shared
layer is the same idea with a file that already exists, and it can hold what
`project.godot` cannot, such as pinning a specific patch release. Read `project.godot`
when the key is absent.

## Custom and locally built engines

Both GUI tools support engines that were never downloaded from GitHub, and Slopworks has
a GDExtension in it, so this is not hypothetical for us.

**Godots** treats every engine the same way. An engine is a name plus a path to an
executable, whether it was downloaded or imported, and a person imports one by pointing
at a directory and picking the binary.

**Godot Launcher** takes a JSON manifest, validated against a schema it publishes. The
required fields are `schema_version`, `version`, `name`, `base_version`, `flavor`,
`config_version` and `platforms`, and each platform entry names a platform, an
architecture and paths. That lets a team hand out one file describing their own build and
have it appear beside the official ones.

The manifest is the better shape for a team, and the directory pick is the better shape
for one person. They are not exclusive.

## What this means for Workbench

Mapping the above onto the rules this repository already has.

**Discovery is one static file.** `https://godotengine.org/versions.json` gives the list
and the release notes URL. No GitHub API on the common path, so no rate limit and no
token. Cache it with a time to live, gdvm uses 48 hours, and fall back to the cached copy
with a warning when the fetch fails rather than failing the page.

**A release's files are read from its manifest, never derived.** This reversed after the
sweep. `godot-builds/releases/godot-<tag>.json` is 8 KB and 0.11 seconds, it states
exactly which files exist, and it carries the SHA 512 that has to be fetched before
installing anyway. Deriving costs a version boundary per change Godot has ever made to its
naming, and there are at least five inside Godot 4. Classify a published name against the
nineteen shapes and drop what does not match. Verify the checksum before extracting, and
refuse rather than warn on a mismatch.

**Nothing here goes near the UI thread.** A version list fetch, a 76 MB download, a 145 MB
extraction and a `--version` probe are all disk, network or process work. This is the
`Read` and `Apply` split from `LauncherViewModel`, at a larger scale, and the download
needs real progress reporting and real cancellation.

**Three services, each behind an interface, and only one of them differs per OS.**

- an engine catalogue, which fetches and caches `versions.json` and derives download URLs
- an engine installer, which downloads, verifies, extracts and registers
- an engine store, which lists what is installed and probes each one for its build string

The per OS parts are small and known: the file name suffix, the path to the executable
inside the archive, the executable bit on Unix, and the console variant on Windows. That
is a factory in `WorkbenchCoreServices` and one implementation per OS, the same shape
`IPathShortener` already has. Nothing else needs to know which platform it is on.

**Install under the data directory, through `IUserDirectories`.** An engine is data the
app fetched, not configuration a person wrote, and not a cache, since throwing it away
costs 76 MB of download. Archives belong in the cache directory, where
`ApplicationPaths.CacheFileFor` already points, and can be deleted freely.

**Strip the archive's common prefix on extraction, with the `.app` guard.** It makes the
.NET and standard layouts identical and retires a naming rule that has already changed
three times. Strip a single top level entry only when it is a directory not ending in
`.app`, since a macOS bundle flattened to the root is destroyed rather than tidied.

**Find the editor in the extracted tree rather than naming it.** On Unix it is the file
carrying the executable bit that is not under `GodotSharp/`, on Windows the `.exe` that is
not the console one, and `--version` confirms the choice.

**Extract with `System.IO.Compression` and chmod as a guard.** Measured above: .NET 10
preserves the mode the archive carries, and the official archives carry `0755`. Shelling
out to `unzip` would add a dependency our own rules forbid assuming.

**Identify with `--version`, never with the file name.** It is 20 ms, it is authoritative,
and it gives the export template folder name for free.

**Leave the person's shell, PATH and desktop alone unless asked.** Workbench launches the
engine itself, so it needs a path and nothing more. Everything GodotEnv does with
`.bashrc`, the `GODOT` variable and the Start Menu is for a tool that has to be reachable
from a terminal. Offering a desktop entry later is fine. Editing three shell files at
install time is not.

## Measured for the engines page, 2 August 2026

Step 1 of `.claude/plans/engine-installs.md`. Run on this machine, on Linux, on a warm
network and a warm page cache. These are the numbers that plan was blocked on.

### The download host answers HEAD, so the size column stands

The open question was whether a build's size can be had without the GitHub API, since
`versions.json` carries no sizes. It can.

A download URL is a 302 from `github.com` to a signed URL on
`release-assets.githubusercontent.com`. The 302 itself carries `content-length: 0`. The
final 200 carries the real one.

```
HTTP/2 302   content-length: 0
HTTP/2 200   content-length: 76056717
             accept-ranges: bytes
             etag: "0x8DEE1D172F2D386"
             last-modified: Tue, 14 Jul 2026 17:58:02 GMT
```

76056717 is exactly the size the GitHub API reports for that asset, recorded earlier in
this document. So **follow the redirect and read the length off the final response**. A
HEAD that stops at the 302 reports zero, which is the way to get this wrong.

**A derived name that does not exist answers 404**, which is what makes deriving URLs
safe rather than hopeful. Measured: a nonsense file name gives 404, and
`Godot_v4.7.1-stable_windows_arm64.exe.zip` gives 200, which is right, since Windows arm64
exists from 4.3 on.

Timings for the six Linux builds of one release, which is what a platform filter shows:

| | Wall clock |
|---|---|
| six HEADs, one after another | 1.34 s |
| six HEADs at once | 0.25 s |

So sizes are fetched in parallel when a card opens, and the column fills a quarter of a
second later. Do not do it sequentially and do not do it for all 33 files of a release.

**The signed URL expires.** Its `se=` parameter was about an hour out. Never cache a
resolved download URL. Cache the `github.com` one, which is stable, and follow the
redirect each time.

`accept-ranges: bytes` with a strong `ETag` is also present, so resume would work whenever
it is wanted. It is deliberately not built yet.

### What the two feed fetches cost

| Fetch | Bytes | Total | Time to first byte |
|---|---|---|---|
| `versions.json` | 48722 | 0.63 to 0.67 s | 0.40 s |
| `godot-4.7.1-stable.json` | 8254 | 0.11 s | 0.11 s |

The version list is slow for its size and it is the website answering rather than a CDN
edge, so the 48 hour cache is doing real work. The checksum file is served by
`raw.githubusercontent.com` and is quick.

That release lists **33 files**, not the 27 recorded further up. The count moves per
release and is not something to hard code.

### The feed's own shape, over all 356 releases

Counted rather than sampled, since step 2 builds a parser against it.

| Label | Count |
|---|---|
| `rc` | 120 |
| `beta` | 94 |
| `stable` | 72 |
| `dev` | 42 |
| `alpha` | 27 |
| `alpha0-unofficial` | 1 |

Five channels, which is what the engines page offers. The design's four are its sample
data.

**The GodotEnv grammar rejects exactly two of the 356**, and both are old:
`3.2-alpha0-unofficial` and `2.0.4.1-stable`, the four component Godot 2 version.
**Every Godot 4 tag parses.** So a reader that drops what it cannot parse loses two
entries from 2019 and earlier and nothing else, which is the right trade for this app.

**All 356 dates parse** as a day month year invariant format with no exceptions.

### A size walk costs nothing worth avoiding

The status bar reports total disk usage, so an install's size is walked rather than
remembered. Measured over the four engines on this machine, third run, warm cache:

| Install | Files | Size | Walk |
|---|---|---|---|
| `4.5.2-stable` | 1 | 131.0 MB | 0.0 ms |
| `4.6.2-stable-csharp` | 74 | 212.4 MB | 0.5 ms |
| `4.7.1-stable-csharp` | 74 | 220.0 MB | 0.5 ms |

A standard install is one file and a .NET install is 74, which is the prefix strip working
as described above. It still runs off the UI thread, since a cold cache, a busy disk or a
network share have no upper bound, but nothing here needs caching or a progress report.

### HttpClient, measured rather than assumed

The downloader is one long lived `HttpClient`, which is what the type is for. A client per
request exhausts sockets, since a closed one leaves connections in TIME_WAIT for minutes
and this app fires several at once when a card opens. The handler takes a five minute
`PooledConnectionLifetime`, which is the half a long lived client gets wrong on its own: a
connection held forever keeps talking to an address that has moved, because DNS is only
consulted when a connection is made.

**`HttpClient.Timeout` does not behave the way it is usually described**, and the design
rests on which way it goes. Measured on .NET 10 against a local server that sends headers
at once and then dribbles a body over nine seconds:

| Completion option | Timeout | Outcome |
|---|---|---|
| `ResponseContentRead`, the default | 3 s | threw at 3003 ms |
| `ResponseHeadersRead` | 3 s | read the whole body, finished at 9006 ms |

So the clock stops when the headers land under `ResponseHeadersRead`. The common warning,
that the timeout covers the body and would cut a large download off partway through, is
wrong for that path. The real gap is the opposite one: **a server that sends headers and
then goes quiet is never ended at all**, because no timer is left running.

That is what the idle guard is for. A linked `CancellationTokenSource` with `CancelAfter`
called again before every read bounds silence rather than the transfer, so a hundred
megabytes over a slow line never trips it and a stalled connection ends. Measured against a
server that sends one block and then stops: the guard fired at 3000 ms.

Automatic decompression is off. The only bodies read are a 48 KB feed and an 8 KB manifest,
so it saves nothing worth having, and asking for it lets a server answer a HEAD with a
compressed length or with none, which would put a wrong size on screen.

`IHttpClientFactory` solves the first two of these and is not used, since it lives in
`Microsoft.Extensions.Http` and `Workbench.Core` takes Tomlyn and the DI abstractions and
nothing else.

### Not measured here

Everything Windows. This machine is Linux, so the file name suffixes, the console
executable and the `%LOCALAPPDATA%` layout are read rather than run.

## Sources

Code read in full or in part:

- [chickensoft-games/GodotEnv](https://github.com/chickensoft-games/GodotEnv), `GodotRepository`, `GodotEnvironment`, `Windows`, `Linux`, `MacOS`, `GodotChecksumClient`, `GodotVersion`, the serializers, `FileClient`, `EnvironmentVariableClient`, `ZipClient`, `ZipClientTerminal`, `GodotVersionSpecifierRepository`
- [adalinesimonian/gdvm](https://github.com/adalinesimonian/gdvm), `releases.rs`, `registry/mod.rs`, `download_utils.rs`, `zip_utils.rs`, `project_version_detector.rs`, `paths.rs`, `app/launcher.rs`, `version/resolved.rs`, `CHANGELOG.md`
- [MakovWait/godots](https://github.com/MakovWait/godots), `sources/github.gd`, `local_editors.gd`, `version_hint.gd`, `zip.gd`, `remote_editor_install.gd`, `config.gd`
- [godotlauncher/launcher](https://github.com/godotlauncher/launcher), `github.utils.ts`, `godot.utils.ts`, `godot.utils.linux.ts`, `godot.utils.windows.ts`, `projectEditorSettings.ts`, `createProject.ts`, `app-config.ts`, `schemas/v1/engine-manifest.json`
- Godot 4.7.1 source at `/home/jason/Projects/godot/godot-src-471`, `editor/file_system/editor_paths.cpp`, `editor/settings/editor_settings.cpp`, `editor/export/export_template_manager.cpp`, `core/os/os.cpp`, `platform/windows/os_windows.cpp`, `platform/linuxbsd/os_linuxbsd.cpp`, `core/version.h`, `version.py`

Endpoints verified live:

- `https://godotengine.org/versions.json`
- `https://godotengine.org/rss.xml`
- `https://raw.githubusercontent.com/godotengine/godot-website/master/_data/versions.yml`, `download_configs.yml`, `download_platforms.yml`, `mirrorlist_configs.yml`
- `https://raw.githubusercontent.com/godotengine/godot-builds/main/releases/godot-4.4.1-stable.json`
- `https://api.github.com/repos/godotengine/godot-builds/releases/tags/4.7.1-stable` and `/4.8-dev2`
- `https://raw.githubusercontent.com/godotengine/godot-interactive-changelog/master/data/godotengine.godot.4.7.1.json`
- `https://registry.gdvm.io/v2/index.json` and `/v2/releases/4.7.1-stable.json`
- the download URLs listed at the top of this document
