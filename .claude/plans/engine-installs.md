# The Godot engines page

The second page in the launcher's activity rail. It manages the Godot builds on this
machine: what is installed, what is available, installing one, importing one that is
already here, and removing one.

Not a Slate stage. It sits beside the thirteen and needs nothing above stage 10, so it
can be built now. Every control it draws already exists in `Workbench.Ui`.

```
dotnet run --project src/Workbench      then pick the Godot item in the rail
```

## Source of truth

Two files in the Claude Design project `8faef49e-39a8-4237-9440-04ab9cf949f4`, read
through DesignSync. Both read whole and both end on `</html>`.

| File | What it carries |
|---|---|
| `Engine Installs.dc.html` | the prototype, its markup and its behaviour |
| `Engine Installs - Spec.dc.html` | the written specification, nine sections |

`.claude/godot-engines.md` is the other half and matters more. It is the research: where
the builds live, how versions are spelled, what the archives contain, what four existing
version managers do and where each one goes wrong. **Read it before writing any of this.**
Nothing in this plan repeats a URL, a file name suffix or a measurement recorded there.

Its section called The landscape is the one to read twice. It is a sweep of all 183 Godot 4
releases and twenty of the archives, and it overturned four beliefs the earlier research
had, including one this plan was built on.

**Scope: Godot 4 and later.** Nothing older is listed or installed.

The design draws a standalone window with its own title bar and status bar. It is a page
in the launcher, so the title bar becomes a page band and the status bar is the launcher's
own, which each page already owns.

## What already exists

- `godot.engines.directory` on `GodotSettingsSchema`, defaulting to
  `ApplicationPaths.Engines`, read through `IGodotSettings`. That is the install root the
  status bar draws.
- The rail item, drawn and disabled, with the `Godot` glyph.
- Every icon the design uses. Measured against `tools/icons/icons.txt`: `search`,
  `refresh-cw`, `folder-open`, `chevron-right`, `check`, `trash`,
  `dots-vertical-rounded`, `arrow-to-bottom`, `x`, `package`, `alert-triangle` and
  `godot` are all in the set. **No icon generation is needed for this page.**
- Every control: `ui:SearchBox`, `ui:Segmented`, `ui:Chip`, `ui:Badge`, `ui:StatusPill`,
  `ProgressBar`, `MenuFlyout`, `ui:DialogWindow`, `ui:DialogFooter`, `ui:Dialog.Role`,
  `ui:Alert`, the toast service and `ui:ToastHost`, the list row states, the status bar
  classes and `StackPanel.emptyState`.
- `EngineViewModel`, the workspace page's engine strip, whose every value is invented.
  This work is what deletes `EngineViewModel.Placeholder`.

## What the launcher gains

Three shell changes, all small and all outside the page itself.

**The rail leads somewhere.** Today `Rail` holds two items with the second disabled and
the right column holds one page. The rail's `SelectedIndex` picks the page, the engines
item stops being disabled, and the workspace page and the engines page become two
children of the same `Panel` with their visibility bound to the selection. The settings
item at the foot stays disabled, since the settings window is its own piece of work.

**A page owns its status bar.** The workspace page already does, since the git strip is a
row inside its grid rather than a row of the window. The engines page brings its own with
the same classes, so nothing in `Themes/Controls/StatusBar.axaml` changes.

**The launcher gains a toast host.** It has held none on purpose, because it had nothing
transient to report. Installing an engine is transient, it finishes while the person is
usually looking at another tab, and it is exactly what the library's rule sends to a
toast. So `App.BuildServices` calls `AddWorkbenchToasts` and `LauncherWindow` puts a
`ui:ToastHost` over the page area. The note in `CLAUDE.md` saying the launcher raises no
toasts is amended rather than left to rot.

## The model

New folder `Workbench.Core/Godot`, beside `Git`. Value objects first, since three of them
close bugs the surveyed tools still have.

**`EngineTag`** is a release tag, `4.7.1-stable` or `4.8-dev2`. Parsing is the only way to
make one, the grammar is the expression in `godot-engines.md`, and it carries the ordering
rule with it: major, minor, patch, then the label rank `dev` below `alpha` below `beta`
below `rc` below `stable`, then the label number. **Patch is omitted when it is zero**, so
`4.7` round trips as `4.7` and never as `4.7.0`. gdvm's table omits alpha and therefore
sorts it wrongly. Do not repeat that.

**Parsing is allowed to fail and the feed is allowed to hold things it rejects.** Measured
over all 356 releases in the feed: the grammar rejects exactly two, `3.2-alpha0-unofficial`
and `2.0.4.1-stable`, the four component Godot 2 version. Every Godot 4 tag parses. So the
catalogue drops what it cannot parse and loses two entries from 2019 and earlier. That also
covers the one release in the feed whose notes URL is empty, since it is the same entry,
but read the field as optional anyway rather than leaning on that.

**`EngineBuildString`** is what a binary prints for `--version`,
`4.7.1.stable.mono.official.a13da4feb`. It is the authority on an engine that is already
on disk: the version, the release kind, whether it is the .NET build, whether it is an
official build, and the commit. Dropping the last two components gives the export template
folder name exactly. A file name is a guess and this is not, so nothing in this page reads
a version out of a file name.

**`EngineBuild`** is one downloadable file: a tag, a platform, an architecture, the .NET
flag, the file name and the URL. The .NET build is a flag on a version and never part of
the version, which is the mistake that costs the surveyed tools the most.

**A build is read from a release manifest and never invented.** This reversed after the
landscape sweep, and it is the largest change the research made to this plan. Godot has
changed its naming at least five times inside Godot 4 alone, every change at pre release
granularity rather than at a minor version, and there are three Linux spellings and two
macOS spellings in that range. Deriving a name means owning all of that and being wrong
each time it moves again. The release manifest states what exists, costs 8 KB and 0.11
seconds, and carries the checksum that has to be fetched anyway. So the naming table is a
classifier: strip `Godot_v<tag>` from a published name, look the suffix up among the
nineteen desktop editor shapes, and drop what does not match. No version logic survives.

Measured over all 183 manifests: 4097 published names, 2316 desktop editors, all nineteen
shapes seen, and everything dropped is an Android editor, the web editor or an export
template pack.

**`EngineId`** is a tag plus that flag, and it is the name for one install. Its text form
is the tag alone for a standard build and the tag with `-mono` after it for the .NET one,
so `4.7.1-stable` and `4.7.1-stable-mono`. Three things take it: the setting that names the
default, the folder an install is unpacked into, and the key a workspace pins its engine
with. That last one is why it is a value object with a canonical text form rather than two
fields carried around together. A person will type it into a config file.

**One install per version, which is why the processor is not in the name.** Other
architectures are installable, so an arm64 machine can fetch the x86_64 build, but a
machine holds at most one build of a given tag and flag and installing another replaces it.
The alternative was putting the processor in the name, and it was tried and dropped: it
makes a pin machine specific, and a pin is shared through a workspace's team config and
read on machines with other processors.

A probe caught the collision that forced the question. With the processor in the name and
the platform absent, the Windows and Linux x86_64 builds of one release both came out as
`4.5-beta2-x86_64`, so 183 of the assertions failed at once. Under this rule they share an
id on purpose, which is what the rule says, and the check that replaced it is that a
release names at most two installs.

Which processor an install actually is gets recorded against the install, since it is worth
showing and nothing needs to name it. **A build string does not carry it.** Measured across
the four engines here: `--version` gives the version, the channel, the module config, the
builder and the commit and stops. For an install Workbench made, the build it came from
says so. For an imported one it is the ELF machine field or the PE COFF one.

The suffix parses without ambiguity even though the tag already holds a hyphen, because a
release label is letters and digits and carries no hyphen of its own. So the last hyphen
in `4.7.1-stable-mono` can only be the flag. Parse the tag first and take what is left.
The word is `mono` rather than `dotnet` or `csharp` because that is what the download is
called everywhere Godot names it, including the file this install came from.

**`EngineRelease`** is a tag, a date, a channel and a release notes URL, with its builds
under it.

**`InstalledEngine`** is a build string, a directory, the path to the executable, the size
on disk, whether Workbench installed it or a person imported it, and the notes URL when
the catalogue knows one.

## What Core owes

Four services, one of them per OS. All registered by a new `AddWorkbenchEngines` in
`WorkbenchCoreServices`, with `TryAdd`, the way every other group is.

**`IEngineCatalogue`** answers what exists, in two steps. `versions.json` gives the release
list, cached under `ApplicationPaths.CacheFileFor` with a 48 hour life. The per release
manifest gives that release's files and their checksums, fetched when a card opens and
cached beside it. Releases below Godot 4 are dropped. It never touches the GitHub API on
the common path: the limit is 60 an hour per address and it counts the person's browser
too, which is the wall gdvm hit and moved off. Both fetches fall back to the cached copy
and say it is stale rather than failing the page.

### The install record

**Every install Workbench makes carries a hidden file naming itself.** One TOML file in the
install directory, through `ISettingsDocumentStore`, so the format lives where every other
format in this app lives and nothing here parses anything by hand.

It holds what cannot be worked out again cheaply or at all:

| Key | Why it is there |
|---|---|
| the id, the tag, the .NET flag | the folder name already says it, and a folder can be renamed |
| the processor and the platform | **`--version` does not carry the processor**, and this is the only record of which build was taken |
| the build string | saves running the binary on every refresh |
| the executable, relative to the directory | the extracted layout differs per platform and per flag, so this is worked out once |
| the source file name and its SHA 512 | says what was verified and against what |
| when it was installed | the Installed tab has nothing else to say it |

That last column is the point. Without the file, a refresh runs `--version` per install, the
processor has to be read out of the ELF machine field or the PE COFF header, and the editor
has to be found in the tree again every time. With it, listing installs is one small read
each and none of that happens.

**It is a cache of facts and never the authority.** A directory changes under an app. If the
file is missing, will not parse, or names an executable that is not there, fall back to
probing and rewrite it. Nothing refuses to list an install because its record is bad.

**Hidden means two different things.** A leading dot hides a file on Unix and means nothing
on Windows, which needs the hidden attribute set instead. So the name carries the dot and
Windows also sets the attribute, which is one more thing `IEngineFiles` answers.

**An imported engine gets no record at all.** Workbench did not create that directory, and
writing into a folder someone else owns is the same overstep as deleting it, which uninstall
already refuses. It turned out to need nothing: the path list in application state is the
whole of it, and everything else is probed on each refresh. Measured at 61 ms for two
engines against 11 ms from records, so a handful of imported engines costs nothing worth
caching. The processor is the one fact a probe cannot recover, and the host's is assumed,
which is right for anything that can run here.

**`IEngineStore`** answers what is here. It lists the engine directory, probes each
candidate with `--version` off the UI thread, and adds the imported engines a person
pointed at. It is the workspace registry's shape: the list of imported paths lives in
`IApplicationState`, and the name, the version and whether the folder is still there are
read from disk on every refresh, so a folder that has gone away shows up without anyone
maintaining a list.

**`IEngineInstaller`** downloads, verifies, extracts and registers, with progress and
cancellation throughout. It runs several installs at once behind a concurrency cap and
each one is cancelled on its own, so the page never has to serialise anything itself.
Verify SHA 512 against the release manifest and **refuse on a mismatch or a missing
checksum** rather than warning, which is GodotEnv's posture and the right one. Cache the
archive with a separate `.done` marker beside it, so a half written archive from a killed
process never looks complete. Extract with `System.IO.Compression` and set the executable
bit on Unix as a guard. Measured on .NET 10: extraction preserves the mode the archive
carries and every Linux editor binary in the sweep carries `0755`, so shelling out to
`unzip` is a dependency we would be inventing.

**The prefix strip carries two guards.** Strip a single top level entry only when it is a
directory whose name is not `.` or `..` and does not end in `.app`. The relative one was
found by a probe: an archive whose entries all sit under `../` looked like a wrapper, so
stripping it turned an escape into an ordinary file and the path check never ran. The rule this plan first had, that the .NET
archive wraps its contents and the standard one does not, holds for Linux and fails
everywhere else: Windows ships the editor and a console executable as two loose files, and
both macOS archives are an application bundle that stripping would flatten and destroy.

**`IEngineFiles`** is what differs per OS, and it is smaller than it was. Naming is not per
OS at all now, since the manifest names the files and the page lists builds for machines
this one is not. What is left is finding the editor in an extracted tree and the executable
bit. On Unix the editor is the file carrying that bit which is not under `GodotSharp/`, on
Windows it is the `.exe` that is not the console one, and `--version` confirms the choice,
which it runs anyway to identify the install. Chosen by the factory in
`WorkbenchCoreServices` beside `AddPlatformIO`, which is the shape `IPathShortener` has.

**`IWebContent`** is the seam over `HttpClient`, with three members: read text, measure a
URL, download to a file with progress and cancellation. Nothing above it names a network
type, and `Workbench.Core` takes no new package for it since `System.Net.Http` is in the
framework. One client, registered once, with a user agent set.

## The page

Four bands, top to bottom, which is section 1 of the spec.

**The head band** carries the Godot mark, the title, a `ui:SearchBox`, Refresh and Add
existing. It is a page band on the page tone, not a title bar, because the window already
has one.

**The toolbar** carries the tabs and, on Available only, the channel selector. Tabs are
`ui:Segmented`, which is an `ItemsControl` of radios, so an option holds a label and a
count side by side and the thumb follows the width when a count changes.

**There is no platform control and no architecture control.** The page shows the builds
this machine can run and says nothing about any other, so there is no choice to offer. That
is the largest simplification in the whole page and it follows from the sweep rather than
from taste. See What one machine sees below.

**The list** is the scrolling body.

**The status bar** reads the install root through `IPathShortener`, then the install
count, the total on disk, and an update marker. It obeys the eight rules the row already
has. **A zero is never drawn**, so the update marker is absent unless the newest stable in
the feed is newer than the newest stable installed, and each readout is colour plus icon
plus label rather than a coloured dot.

### The two lists, and why they are `ListBox`

Every list surface virtualises, and the feed holds 74 releases with up to 27 builds each.
Both tabs are a `ListBox` with `SelectionMode="None"`, since only `ListBox` replaces its
panel with a virtualising one in Avalonia 12 and a card is not a selectable row.

`ui:Tree` was considered and is wrong here. The Available tab is a tree in shape, release
over platform group over build, but a tree row is one line and a release card is a header,
a body and a footer. The card's own build rows are a plain `ItemsControl`, bounded at 27
and only realised while the card is open.

**Available.** One card per release, collapsed except the newest. A build row is the file
name in mono, a `.NET` chip for the Mono build, the size, and one of four right hand
states: an Install button, a queued label, an inline `ProgressBar` with a percentage and a
cancel, or a muted Installed check.

**A card holds two rows until the disclosure is opened**, the standard build and the .NET
one, and one row for the sixteen 4.0 alphas published before the .NET build existed. Open,
it holds every architecture the platform published, grouped, host first. The platform
headings the design first drew are gone, replaced by architecture headings, and so is the
count of builds a filter is holding back, since the only filter left picks whole cards
rather than rows.

**Several downloads can run at once**, which is a change from the spec. Each row owns its
own progress and its own cancel, and starting one never touches another. Three run at a
time and the rest queue, so clicking Install down a whole card does not open twenty
sockets and leave every bar crawling. The cap is a judgment rather than a measurement and
it lives in one place. A queued row can be cancelled without ever having started.

**Installed.** One row per install, with pills for the facts not in the title: `DEFAULT`,
the channel when it is not stable, and `.NET`. The subtitle is the path, the size, the
platform and processor, and the workspaces using it, drawn as separate parts rather than one
joined string, which is what the design changed to.

**The processor there is the part that earns its place**, since other architectures are
installable and the row is the only thing that says which one was taken. The platform beside
it says the same thing on every row and is kept only because the design draws it.

Actions are Set default, Uninstall, and a three dot `MenuFlyout`.

Both sort by release date, newest first, ties broken alphabetically. An install takes its
date from its tag, so a build installed today still lands in version order.

### Uninstall

A `ui:DialogWindow` with `ui:Dialog.Role` on both buttons, so Enter and Escape are
Avalonia's and the caller wires nothing. The path and the size sit in a `SurfaceWell`
border as evidence. The consequence line changes with use, naming the workspaces that
reference the install when any do.

**An imported engine is forgotten, never deleted.** Workbench did not put those files
there and they sit outside the engine directory, so removing one takes it out of the list
and leaves the disk alone. The dialog says so, which means the design's line about files
being deleted is one of two lines rather than the only one. This is the one rule in the
page that can destroy something a person owns, so it is worth stating twice.

### The three dot menu

Open install folder goes through `IPlatformServices.Open` with a `DirectoryLocation`.
Copy path is the clipboard. Release notes opens the URL from the catalogue and is hidden
when there is none, which is every imported engine and any custom build.

**Verify checksum is dropped, and this is a finding rather than a trim.** The published
SHA 512 is over the zip archive, and after a successful install the archive is a cache
entry that can be deleted at any time. So the menu item cannot do what it says on an
extracted tree. Verification stays where it belongs, at install time, refusing on a
mismatch. If re verification is wanted later it is a manifest written at install time and
re hashed against the tree, which detects local corruption and nothing else, and that is a
different feature with a different name.

### Toasts

Two, both from the spec, both raised through the injected `IToastService`.

Install finished is the success tier with a `Show in Installed` action. Install failed is
the error tier and stays until dismissed, with a Retry action, and the row goes back to
Install.

**Dwell is the library's, not the design's.** The design says six seconds. `ToastOptions`
says four, eight when there is an action, and indefinite for an error. The success toast
has an action, so it gets eight. One dwell rule in the app beats two, and the number lives
in one place either way.

Nothing else is toasted. Setting the default, changing a tab or a filter, opening a card
and download progress are all already visible where they happen.

### Alerts and empty states

A stale catalogue is an alert and not a toast, because it describes the state of what is
on screen rather than something that just happened. `ui:Alert` in its strip form under the
toolbar, saying the list is the cached one and when it was read, with a Retry action.

No rows after filtering is `StackPanel.emptyState` with the package glyph, which is the
design's fourth alert form and was already built.

## Departures from the design, and why

| Design | Here | Reason |
|---|---|---|
| A standalone window | A page in the rail | It is the engines page, and the launcher already owns the frame |
| Six platforms including Web and Android | No platform control at all | Now the design's own rule. Spec 3 states it |
| A specific cause for a withdrawn release | A generic sentence | The feed and the manifest say only that files are absent, never why. See below |
| An install marked per file | Marked per version | One install per version, so a second processor replaces rather than joins |
| One download at a time | Three at a time, the rest queued | Asked for. Each row owns its progress and its cancel |
| Opens on Available | Opens on Installed | Asked for. The page is opened to see what is here more often than to fetch something new |
| Channel as selectable chips | A second `ui:Segmented` | `ui:Chip` has no selected state and channel is one choice among several, which is what a segmented row is |
| Four channels | Five, from the feed | Counted over 356 releases: `rc` 120, `beta` 94, `stable` 72, `dev` 42, `alpha` 27. The design's list is its sample data |
| Verify checksum in the menu | Removed | The published hash is over the archive, which is gone after install |
| Six second success toast | The library's eight | One dwell rule, and `ToastOptions` already holds it |
| Simulated downloads | Real ones | Named in the design as a prototype affordance |

## What one machine sees

The page is scoped twice, and the two limits are not the same shape. **Platform is a wall
and architecture is a default.** Section 3 of the spec states both.

**Only this machine's platform is ever offered.** There is no platform control, no macOS
download on a Linux machine, and no way to reach one. A build for another operating system
cannot be launched here, so listing it would be an Install button with nothing behind it.

**Only this machine's processor is shown, and the rest are one click away.** Every other
architecture the host platform publishes stays installable. It is simply not the first
thing on screen, because for almost everyone it is noise. Cross compiling is a real reason
to want one, so nothing is taken away.

Read the processor from the operating system rather than from this process, since Workbench
ships as x64 only and a Windows machine on arm64 runs it under emulation while still
wanting the native arm64 editor. `RuntimeInformation.OSArchitecture` answers that and
`ProcessArchitecture` does not. `IEngineFiles` reports both, so nothing else asks.

### The disclosure

An open card groups its builds by architecture, **the host one first**, and the footer
carries the toggle: `Show 2 other architectures`, then `Show x86_64 only` to put them back.

**The choice is global and it resets on relaunch.** A machine either cross compiles or it
does not, so the answer holds while a person moves between releases rather than being asked
again per card. It is view state and never a setting, which is why nothing writes it down.

The count in the label is per card, since a release does not always publish the same set.
Measured: Linux ARM starts at 4.2-beta5 and Windows arm64 at 4.3-rc1, so on an x86_64 host
the label reads three others for a recent release and one for an early one.

### Three card states, not one

**Builds for this machine.** The ordinary case. One group, the standard build and the .NET
one, and one row alone for the sixteen 4.0 alphas published before the .NET build existed.

**Nothing at all for this platform.** Spec 5.1, and the design draws it fully: the card
keeps its place in date order and stays expandable, its title drops to the muted tier, the
build count leaves its subtitle, and a muted info marker on the right reads
`No Windows builds`. Open, the body replaces the groups with a headline restating the
absence and a mono line naming the platforms the release did ship for. The sentence giving
the cause is dropped, for the reason under The card never says why. The release notes link
stays, nothing is installable, and no error tier is used, because this is information
rather than a failure.

**Builds for this platform but none for this processor.** The design does not draw this one
and it is the most common of the three on an ARM machine. Measured: a Windows arm64 machine
has no native editor for any of the 81 releases before 4.3-rc1, and a Linux ARM machine has
none for the 101 before 4.2-beta5, while both have x86 builds all the way back. Today that
card opens with no rows at all and a footer offering the other architectures, which reads as
a bug. It wants 5.1's treatment scaled down: a muted line reading `No arm64 build in this
release` where the groups would be, with the disclosure under it.

**Falling back to an x86_64 build on an arm64 machine is still not done.** Windows would run
it under emulation. The disclosure already reaches it in one click, which is the honest
version of the same thing, and it leaves the choice visible rather than silently installing
an architecture nobody asked for.

**One install per version changes what Installed means on a build row.** A machine holds at
most one build of a tag and .NET flag, so once a version is installed every row for it reads
Installed whatever processor that row is, and the Installed tab says which one was taken.
Swapping processor is an uninstall and an install rather than a second row appearing. The
design marks only the exact file installed, which is the older rule and would show one row
Installed and its neighbour offering an Install that replaces it without saying so.

## The card never says why

Spec 5.1 gives an absent release a sentence explaining the cause, and the prototype writes
one: binaries withdrawn after a signing failure. **That line is dropped.** Nothing in the
feed or in a manifest says why a file is absent and no endpoint does, so the only way to
write it would be a hand maintained list of excuses that goes stale the first time it is
not updated. A card that guesses a reason is worse than a card that states a fact.

What stays is the fact and the way out: the headline naming the absence, a mono line naming
the platforms the release did ship for, and the release notes link, which leads to the one
place that might explain it.

That mono line is answerable and needs one small extra. The classifier keeps only desktop
editors, so by the time a card is built the macOS, web and Android names have been dropped
and there is nothing left to name. The catalogue keeps the set of platforms seen in the
manifest, including the ones it cannot install, purely so that line can be written.

## The gap the design cannot fill, and how it is closed

**`versions.json` carries no file sizes**, and the design draws a size on every build row.
The checksum file carries names and hashes only. The GitHub releases API carries sizes and
is the thing we are staying off.

**Measured on 2 August 2026, and the column stands.** The download host answers `HEAD`
with the real `Content-Length`, but only on the final response: the `github.com` URL is a
302 to a signed URL and the 302 itself reports zero. So follow the redirect and read the
length off the 200.

A card needs two of those while the disclosure is closed, since one machine sees one pair
of builds, and eight on Linux or six on Windows once it is opened. They go at once.
Measured through the catalogue: eight Linux sizes for one release took 169 to 309 ms in
parallel, against 1.34 seconds for six done one after another. The column is blank until
it answers and never blocks the row.

**The signed URL expires in about an hour**, so a resolved URL is never cached. The
`github.com` one is cached and the redirect is followed each time.

## Scrolling is imprecise while a card is open

**Measured and left alone on purpose.** A card that opens is much taller than one that is
closed, and `VirtualizingStackPanel` estimates its extent from the rows it has realised, so
the same list reports an extent anywhere between 1730 and 6196 depending on where it is
scrolled. The thumb changes size as it moves and a scroll past the estimate stops short.

It is not this page's to fix. `.claude/plans/variable-height-list.md` is the panel that
fixes it, and stage 11's grids need the same thing. Until it lands the page keeps the
built in panel, since the alternative is realising all 183 cards, which is the 348 to
709 ms this already came back from.

## Hazards

**The engine directory can be anything.** It is a setting, it is rooted and that is all
the rule checks. It can be missing, unwritable, on a full disk, or on a different mount
from the cache. Each of those has an answer and none of them is a crash.

**An archive is 70 to 106 MB and an extracted engine is 137 to 145 MB.** A download, an
extraction, a probe and a size walk are all disk, network or process work, so all of it is
`Read` off the thread pool and `Apply` on the UI thread, which is `LauncherViewModel`'s
split at a larger scale.

**Extraction is attacker facing.** Reject an entry whose path escapes the target after the
prefix strip, and reject one that decompresses past its declared size. Both are gdvm's and
both are cheap.

**Two installs of one minor version share Godot's editor settings.** Measured: 4.7.0 and
4.7.1 share `editor_settings-4.7.tres`, and the .NET and standard builds of one version
share everything. Nothing here changes that, and it is worth knowing before anyone reports
it as a bug in this page.

**Export templates are 1.9 GB per version and are not counted.** They live in Godot's own
data directory rather than ours, and uninstalling an engine here does not touch them. The
status bar's number is what Workbench installed and what Uninstall would delete. Saying
otherwise would put a number on screen that no action in the page can change.

## Cross platform

Answered per the three questions in the root `CLAUDE.md`, and only the first column varies.

- Which file in an extracted tree is the editor. Windows ships two and the plain one is
  the one to launch, not the console executable. `IEngineFiles`.
- The executable bit, Unix only, as a guard after extraction.
- Opening a folder and a URL, which `IPlatformServices` already answers.
- Comparing paths, which is `IPathRules`, since Windows folds case and Linux does not.
- Displaying the install root, which is `IPathShortener`.

Nothing else tests the running OS, and no call site tests it at all.

**Not done here, deliberately.** No symlink, no PATH entry, no shell file, no desktop
entry and no Start Menu shortcut. Workbench launches the engine itself, so it needs a path
and nothing more. GodotEnv edits three shell files at install time and its own message
admits it misses fish.

## Decided

Four questions were open when this was written. All four are answered.

**Other platforms are not shown at all.** This went further than the first answer, which
kept Windows, Linux and macOS with the host first. The page lists what this machine can
run and nothing else, so the platform control and the architecture control both leave the
toolbar. See What one machine sees.

**Progress stays inline on the build row and nothing follows it.** Leaving the Available
tab hides it, and the finish toast is what reports the outcome, which is the case that
toast exists for.

**Several downloads run at once**, three concurrently and the rest queued. This replaces
the spec's one at a time, and it deletes the spec's rule that starting a download cancels
the running one. Each row cancels only itself.

**The default install is named by `EngineId`**, the tag plus the .NET flag, stored in
`godot.engines.default` on `GodotSettingsSchema` and resolved against the store on read.
When it resolves to nothing there is no default, and choosing the next one is a person's
act rather than the app's. The same value object becomes the workspace engine pin later,
which is why it carries a canonical text form rather than living as two fields.

**Download resume is not built now.** A killed download restarts, which costs 70 to 106 MB
of network and nothing else. The `.done` marker is in from the start regardless, so a half
written archive never looks complete. gdvm's `ETag` and `Range` with `If-Range` can land
later without changing anything above the installer.

## Build

Seven steps. Each one is checkable on its own and the numbers are the order.

1. ~~**Measure the unknowns.**~~ **Done, 2 August 2026.** The answers are in
   `godot-engines.md` under Measured for the engines page. `HEAD` gives the size on the
   final response, the feed costs 0.63 s and the checksum file 0.11 s, a size walk is
   under a millisecond, five channels exist, and every Godot 4 tag in the feed parses.
2. ~~**The value objects.**~~ **Done, 2 August 2026.** `EngineTag`, `EngineId`,
   `EngineBuildString`, `EngineBuild`, `EngineRelease` and the two enums, in
   `Workbench.Core/Godot`. Checked by a probe: 5783 assertions, the whole 356 release feed
   round tripping, all 183 Godot 4 manifests classified, and the build strings read off
   the four engines on this machine.
3. ~~**`IEngineCatalogue`** and `IWebContent`.~~ **Done, 2 August 2026.** `IWebContent` in
   `Platform` with its three members, and the catalogue with both caches, the stale answer,
   the Godot 4 floor and the published targets line. Checked by a probe over a real
   composition root: 183 releases fetched in 990 ms and read back from cache in 2 ms,
   16 desktop editors and 33 checksums for 4.7.1, the 4.0-alpha1 spellings, eight Linux
   sizes in 169 ms, a download with progress, and an unreachable network answering from
   cache and saying so.
4. ~~**`IEngineStore`** and `IEngineFiles`.~~ **Done, 2 August 2026.** The record and its
   one home, listing, probing as the fallback, importing, the imported list in application
   state, and `godot.engines.default` on `GodotSettingsSchema`. Checked against real engine
   trees copied out of gdvm: two installs read and recorded, a record written again after
   being emptied and after being corrupted, an import that writes nothing into a folder it
   does not own, an uninstall that deletes and a forget that does not.
5. ~~**`IEngineInstaller`.**~~ **Done, 2 August 2026.** Download with progress and
   cancellation, three at once behind a cap, the `.done` marker, SHA 512 verification, the
   guarded extraction with the prefix strip and its `.app` exception, and the executable
   bit. Checked by installing a real 4.7.1 editor end to end, reinstalling it from cache in
   303 ms, refusing a wrong checksum and a missing one, and refusing an archive that
   escapes its directory and one that claims to unpack to 2861 MB.
6. ~~**The page shell.**~~ **Done, 2 August 2026.** Rail switching, the two pages, the head
   band, the toolbar, the filters, the status bar, the empty state, the stale alert and a
   plain row per release or install. No install actions, no card body, no dialog and no
   menu, which are step 7. Checked by rendering the real window headless.

   **The sort is by release date, which the code had wrong.** Spec 4 says date and the
   catalogue sorted by version. The two really differ: 4.5.2 shipped in March 2026 and 4.6
   in January, so version order buried a newer release under an older one. Fixed, and the
   update marker still reads the highest version rather than the most recent date, since a
   patch of an older line is not an update however recently it landed.

   **The list is a `ListBox`, because it is the only items control in Avalonia 12 that
   virtualises.** It was an `ItemsControl` first and picking a channel cost 348 to 709 ms,
   which is 20 to 40 dropped frames, because all 183 releases were realised. Measured after:
   44 to 65 ms, 8 or 9 containers realised, and the cost no longer grows with the row count.
   The rows are also filled through one `AddRange` rather than a clear and an add per row,
   which took the projection itself from 129 ms to 4. This is the rule the plan already
   states, broken and then put back.

   Rows arrived a step early. The plan had the list in step 7, but an empty body under a
   tab reading 183 says two different things at once, so the shell got the row and step 7
   keeps the card.
7. **The cards and the actions.** Split in two, since the tabs are independent.
   - ~~**Available.**~~ **Done, 2 August 2026.** The release card opening onto its builds,
     grouped by processor with the host first, sizes fetched together when a card opens,
     install with a queued state, a bar, a percentage and a cancel, the architecture
     disclosure, the absent release card, and the two toasts. The launcher gained its
     first toast host.
   - **Installed.** Set default, the uninstall dialog, the three dot menu and Add
     existing.

Then the payoff, which is why the strip is worth wiring in the same piece of work:

8. **Wire the workspace page's engine strip** to `IEngineStore` and the version
   `project.godot` asks for, and delete `EngineViewModel.Placeholder`. That is the last
   invented data in the app.

## What proves it works

- A release list that came from the network, and the same list from cache with the network
  off, drawn with the stale alert.
- An install that completes end to end, appears in Installed, and raises its toast while
  the Available tab is showing.
- An install refused on a checksum that does not match, with the row back on Install.
- Four downloads started together, three running and one queued, one of them cancelled
  without disturbing the others, and the queued one starting when a slot frees.
- An engine imported from a folder Workbench did not write, uninstalled, and the files
  still on disk afterwards, with nothing of ours left in the folder.
- An install whose record is deleted, listing correctly from a probe and writing the record
  back, and one whose record names an executable that is gone, doing the same.
- An engine Workbench installed, uninstalled, and the directory gone.
- The default uninstalled, leaving no default and nothing chosen automatically.
- The tag grammar over its awkward cases: `4.7`, `4.7.1-stable`, `4.8-dev2` and `4.8-rc1`
  parsing, sorting and round tripping, with `4.7` never becoming `4.7.0`.
- The Windows half read rather than run, and said so plainly, since this machine is Linux.
