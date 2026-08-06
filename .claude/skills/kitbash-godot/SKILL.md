---
name: kitbash-godot
description: "Kitbash Godot integration. Engine version patterns and resolution, where a project's engine requirement comes from, the build then import then start pipeline, import progress, and verified constraints about the Godot side. Read before changing engine matching or project launching."
---

## The engine a workspace needs

Which Godot opens a project, and how the answer is worked out. All of it is
`Kitbash.Core/Godot`, composed by `AddKitbashGodotProjects`.

**A version a project asks for is not a release, so it has its own type.**
`EngineVersionPattern` is a major with an optional minor, patch and channel, and a part
left out matches anything. `4.7` answers to `4.7-stable` and `4.7.1-stable` alike, where
an `EngineTag` always knows its patch and its channel and cannot hold the question.

The two cannot spell the same thing. A tag writes a zero patch as nothing, so
`4.7-stable` means patch zero, while here a missing patch means any patch and only
`4.7.0-stable` pins it. **Anything meaning one exact release holds an `EngineTag`**, and
the pattern is for the loose case, which is the only case a project file produces.

**A pattern that names no channel prefers a stable release over any prerelease.** With
4.7.1-stable and 4.7.2-rc1 installed, `4.7` opens the stable one. Taking the highest
outright is the obvious rule and it is wrong: a candidate is not an upgrade from a
release, and somebody who wants one names it. A pattern that does name a channel has
already said so, so there the highest wins.

**A pin can end in `-mono`, which is how an install is named.** So `godot.engine` takes
the exact text the engines page calls a build, `4.7.1-stable-mono`, and it means that
release and the .NET one. The suffix is asymmetric on purpose: writing it requires the
.NET build, and leaving it off requires nothing, since a plain build carries no suffix
and a missing one cannot mean "not .NET" without making `4.7` mean it too. Whether a
project needs C# is a fact read off the project, and a pin can only add to it.

### Where the requirement comes from

`IEngineRequirementReader`, in this order, and the first that answers wins:

1. `godot.engine` in the workspace's own `.kitbash` config, personal layer over team
   shared. Described by `WorkspaceGodotSettingsSchema`, which is the one Godot page a
   team shares, since which engine a project needs travels with the project.
2. `config/features` in the project's `project.godot`.

**The deliberate answer beats the incidental one.** Godot writes `config/features`
itself and it records the version that last opened the project, so it drifts every time
somebody opens it in something newer. The `.kitbash` key is written by a person.
A blank key is not an answer and falls through rather than masking the layer below.

**The flag never comes from the config**, only ever off the project, even when the
version came from the workspace.

`IGodotProjectReader` reads that file. It is not TOML and not quite INI: keys carry
slashes and values are written in Godot's own syntax, so it is read by line with the
section tracked and only the few keys that are wanted are understood. C# is said two
ways and either counts, a `[dotnet]` section (`[mono]` in Godot 3) or `"C#"` among the
features. `WorkspaceNameResolver` takes the name from the same reader, so the four level
search for `project.godot` exists once.

### Matching it against what is installed

`IEngineResolver` is pure. It is handed the requirement and the list rather than reading
either, so the rules are checkable with no disk. The order is the right version with the
right runtime, then the right version with the wrong runtime, then this machine's
default, then nothing.

**The runtime is filtered on first, not checked afterwards.** A .NET project will not
build in a plain engine, so a match on version alone would send somebody into an editor
that cannot compile their code. An engine whose folder has gone answers nothing and
cannot be the fallback either.

Four answers, and `EngineViewModel` turns one into the strip's words:

| `EngineMatch` | The strip |
|---|---|
| `None` | no Godot project here, and no button |
| `Matched` | the engine's name and where it is, Open in Godot |
| `Mismatch` | the engine that would open it, why it is wrong, Open anyway |
| `Missing` | what was asked for, not installed, Install |

**The strip follows the engines page.** `EnginesViewModel.InstallsChanged` fires after
every read there, and every mutation on that page ends in a read, so one event covers
installing, uninstalling, importing and naming a default. Installing the version a
workspace asks for turns the strip green without anybody switching workspaces to make it
notice. It is the same reason the git strip watches a repository, with the difference
that engines are changed inside this app rather than outside it, so there is nothing to
watch and no beat to run.

**On a mismatch the button installs what was asked for.** Opening the wrong engine is the
workaround, so it moves into the menu as Open anyway, which is the one state where that
item appears. A mismatch is not a state to settle into.

**It installs in one press rather than going looking.** A workspace that names a version
has already said which engine it wants, so pressing Install picks the release the same
way the resolver picks an install, opens its card and starts the download. The page is
where an install already shows its bar, its cancel and its toasts, so the strip starts
one there rather than owning a second way of reporting one.

**The filter is left alone and the card is scrolled to instead.** Typing into somebody's
search field on their behalf takes away everything else they might have wanted, and they
asked for an engine rather than for a filtered list. Scrolling is `Reveal`, which the
page supplies the same way it supplies the clipboard, the folder picker and the confirm.

**The strip shows an engine the way an engine is named**, whether one is installed or
only asked for: the numbers alone as the title, then the channel and the .NET flag as
pills. So a workspace pinned to `4.7.1-stable-mono` reads Godot 4.7.1 with STABLE and
.NET beside it, rather than 4.7 or a raw string. The channel pill shows for stable too,
unlike a row on the engines page, because a row there sits among others and a missing
pill reads as stable by contrast, where this is one line with nothing to contrast with.

**The strip names the engine that would open the project, not what was asked for**,
except when there is no engine to name. Reading 4.7 while 4.6 is what opens would be
worse than saying nothing. The `.NET` badge follows the same rule and describes the
engine whenever one is named, since beside a plain engine's version it would otherwise
say the opposite of what is wrong.

The strip is read outside `LauncherViewModel.Read`, which runs before the window exists
and has to stay quick. This one asks the disk about every install and can run a process,
so it has its own async refresh and the strip says it is reading until that lands. It
runs one at a time and **waits its turn rather than giving up**, since dropping the
second would leave the strip describing the workspace that was open a moment ago.

### Making a project

`IGodotProjectWriter` writes what Godot's own project dialog writes, and the reference is
`ProjectDialog::ok_pressed` and `ProjectSettings::_save_settings_text` in the 4.7.1
source. Read those before changing a byte of it.

Four files. `project.godot`, `icon.svg`, `.editorconfig`, and on request the `.gitignore`
and `.gitattributes` Godot writes for a project in git. **Every one of them is LF on both
platforms**, because Godot's `FileAccess::store_line` is, and the `.gitattributes` it
writes says `* text=auto eol=lf`.

`project.godot` is written by hand rather than through a settings document, since it is
not TOML and not quite INI. The header, the `config_version=5`, the section order and the
key order inside each section are all what Godot produces, so a project made here reads
the same as one made in the editor. The settings are the dialog's own plus
`EditorNode::get_initial_settings`, which is the stretch mode, the stretch aspect, Jolt
and the Windows rendering driver.

`config/features` is the engine's major and minor plus the renderer's name, sorted, which
is why the writer is handed an `EngineTag` rather than working one out. **Nothing here
needs an engine to run**, so a project can be written for a version that is not installed
yet.

`icon.svg` is `DefaultProjectIcon` from the editor icon set, copied byte for byte. It is
not ours to redraw.

Measured on this machine: the generated project imported under real 4.7.1 headless with
exit 0 and no errors, the icon was imported, and the engine changed nothing in the file it
was handed. The `icon.svg` written matches `editor/icons/DefaultProjectIcon.svg` exactly.

**Compatibility is the one renderer that writes two keys.** It also overrides the mobile
default, since the mobile renderer would otherwise take over there. That is Godot's rule
and it is the only asymmetry between the three.

**The workspace is pinned to the engine it was made with**, through `godot.engine` in the
team config, so opening it later never picks a different one. The pin is the plain tag,
never `-mono`, since a project made here has no C# in it.

### Opening a project

`IGodotLauncher` does three things in order, and reports each one so a dialog can say
which is running. The flags are read off `--help` on 4.7.1 rather than remembered.

1. **Build the C#**, when the project has C# in it and the engine is the .NET build.
2. **Import the assets**, when anything a sidecar declares is not on disk.
3. **Start it**, through `StartDetached`.

**The first two are waited for and the third is not.** Both have to finish before the
third is worth starting, and what starts is the thing a person is waiting for.

**Running is the default and editing is the flag.** Godot 4 runs the project when it is
handed a path, and `--editor` is what asks for the editor instead. So Open in Editor is
`--path <folder> --editor` and Play is `--path <folder>` with nothing added. Measured on
4.7.1: nothing in the 129 lines of `--help` matches `-g` or `--game`, and the editor flag
reads "Start the editor instead of running the scene."

**Clean rebuild is a third mode.** It deletes `.godot`, builds, imports unconditionally,
and then stops. It is for the case where Godot itself is what is wrong rather than the
project, which is what a stale cache looks like.

It throws away more than imported assets. `.godot` also holds the uid cache, the script
class cache and the editor's own per project state, and a rebuild is asked for precisely
when one of those is the thing that is broken. All of it is Godot's to make again and
none of it is committed. **The folder is named, never searched for**, so there is no case
where this deletes something it found.

**Two files in there are kept: `editor/editor_layout.cfg` and
`editor/project_metadata.cfg`.** The first is where a person put their docks. The second
is the editor's own record of this project: the scenes that were open, the last one
edited, the folders the file dialogs were left in, the state of the panels. Both are
things a person arranged rather than Godot generated, and both sit among files that are
safe to delete. They are read before the delete and written back after the import, in a
finally, so a rebuild that failed or was cancelled does not take them as well. Writing
them back afterwards rather than before means whatever the headless editor did on its way
past cannot win, which is a guard rather than a fix: measured, a headless import writes
no layout of its own. A failure putting one back is swallowed, since losing a dock
arrangement is a bad afternoon and losing the rebuild over it would be worse.

`KeptFiles` in `GodotLauncher` is the list, and each entry is a path under the cache
written with forward slashes. **Adding a third is adding a line to it**, since the read,
the delete and the write back are all over the list.

**Neither file being there is the ordinary case and not a problem.** Godot writes both
when a person closes the editor, so a project that has only ever been imported has
neither and a fresh clone has neither. Then nothing is kept and nothing is put back,
which leaves the project exactly as it would have been. A file that exists and cannot be
read is treated the same way, and the two are handled one at a time, so one missing does
not stop the other being kept.

**It does not open the editor when it finishes.** The dialog stays up and offers Dismiss
and Open in Editor, and pressing the second goes the ordinary way rather than starting
the editor from there, so the build is confirmed to still be good rather than assumed
from a moment ago.

Otherwise `GodotLaunchMode` is read by the last step and nothing else. A project that
will not build will not run either, and one that was never imported has no resources to
run with, so editing and playing do the same work up to that point.

### Getting out of the way after it

**The launcher can get out of the way once Godot is running, and it does not unless it
was asked to.** Three settings, each doing nothing until it is set, one per thing that
starts: the project manager the engines page opens, a project opened in the editor, and a
project run. Each holds an `AfterLaunchAction`, so the answer is do nothing, minimize or
close. They are the launcher's own keys and the `kitbash-settings` skill has them.

**It only ever acts after something really started.** A failure has already thrown, and
a launch that was cancelled part way started nothing, so `LaunchDialog` reports which of
the two happened rather than answering one bool. That is `GodotLaunchOutcome`, and the
rebuild's Open in Editor is its third answer, which goes round again and leaves the
action to the second pass. A rebuild itself does nothing, since it starts nothing.

Measured against real projects rather than written logs. A broken build in both launch
modes: it throws, the import never runs, nothing starts and nothing lands in the project.
A real rebuild twice over: the first found no cache and made one of 7 files, a marker
file put inside it was gone after the second, proving the folder itself went rather than
its contents, the engine made it again, and the project read as imported both times with
nothing ever started.

**A plain engine never builds.** A project's C# only builds against the .NET build, so
asking a plain one fails and asking dotnet produces assemblies that engine will not load.
When the two disagree the strip has already said so, and opening anyway opens what is
there.

`godot.build` picks the program. Automatic takes dotnet when this machine has one, which
is what `IExternalTools.Dotnet` was resolving for and nothing used, and the editor
otherwise through `--build-solutions --quit --quiet --no-header --headless`. `--quit` is
what makes it exit, since `--build-solutions` implies `--editor`. dotnet builds the
solution, or the project file when there is no solution, and no solution at all is not a
failure.

**The import is not left to the editor, which is a departure from every launcher read.**
Godot imports on open by itself, so this is not work being saved. Doing it first means the
wait happens under a dialog that says what is running and can be cancelled, instead of the
editor sitting on a splash screen for minutes on a fresh clone. It is skipped outright
when nothing needs importing, which is every open after the first.

**Whether an import is needed comes from the sidecars, never from the cache folder.**
`IGodotImports` walks for `.import` files and checks that every generated file each one
declares is on disk. The sidecars are committed and the generated files are not, so a
fresh clone has all of one and none of the other. The folder can also be present while
its contents were cleared. Only the generated paths count, under `.godot/imported` for
Godot 4 or `.import` for Godot 3, since a sidecar also names its source file and its uid
and neither says whether an import has run. Measured on the real game project: 399
sidecars among 6560 files, answered in 20ms.

**A step that fails stops everything and the editor never opens.** Opening anyway would
put somebody in an editor whose assemblies are stale or missing, which is the failure the
build exists to catch, and it would do it silently, since the editor has no idea a build
was attempted. Turning the build off is a setting and a deliberate act, so the failure
dialog offers no Retry and no Open anyway. Measured against a real broken project: the
build throws, the import never runs, the editor never starts and nothing is written into
the project.

**The failure dialog shows the errors and copies the log**, which are deliberately two
different things. Measured on a two error build, dotnet writes 13 lines, opens with
restore chatter and prints every error twice, once inline and once in its summary, so the
well shows the error lines deduplicated and falls back to everything when none stand out.
The button takes the whole log and says which log it took, since the well is showing
something shorter. It confirms on itself rather than through a toast, because a toast
belongs to the window behind the modal.

**The import reports real progress, because Godot prints its own.** Every step goes to
standard output as `[  45% ] reimport | big_395.png`, with a `Started ... (520 steps)`
line ahead of each phase, and it arrives while the work runs rather than at the end.
Measured with timestamps on a 460 asset import: the lines came out across the run, and
196 of a captured 208 carry progress. `GodotProgressReader` reads them and
`IProcessRunner.ReadLinesAsync` is what delivers them a line at a time.

Two things that reading has to correct for, both measured rather than guessed. **Short
phases either side of the work are ignored**, since opening the project and loading the
editor declare five steps each and would sweep the bar again at both ends. The test is a
step count rather than a phase name, because a name is Godot's own identifier and the
real question is whether a phase is the work or the startup around it. **The finish is
reported by the launcher**, since Godot's last phase declares the whole count and then
takes one step, which left a full bar reading almost nothing.

**The two phases are told apart and the item is shown.** Scanning and importing both
declare the asset count and both are real work, so the dialog names which one is running
and the mono line carries the count and the file being read, such as
`312 / 520  big_311.png`. The scan is the one task name read, and anything else long
enough to report reads as importing, so a rename costs a word rather than the progress.

**Godot's own narration is left out and its items are kept.** A line carries one or the
other and the two are told apart by the trailing dots: measured over a whole import,
every message that is a sentence ends in them and no file name does. The narration says
nothing a person waiting for their project needs, and the line it would go on is mono and
holds values.

A line it cannot read is skipped and nothing fails. An older engine that prints none of
this leaves the bar indeterminate, which is what it was before.

`LaunchDialog` is the design's Progress kind and `LaunchFailedDialog` its Error kind.
The bar is indeterminate for a build and for anything that reports no fraction, since
neither a C# build nor a start says how far through it is. Its close glyph stays live where the design flattens
it to disabled, because closing and Cancel do the same thing here and a dead glyph would
refuse a gesture the dialog already honours. The second has no Retry, since after a failed
build the next thing to do is fix the code.

## Verified constraints about the Godot side

These were established by reading Godot 4.7.1 source at
`/home/jason/Projects/godot/godot-src-471` and by running probes. They are
expensive to rediscover.

- **GodotSharp cannot be used outside the engine.** Its native calls resolve
  against the host process, so with no engine present `new Resource()` and
  `new StringName("x")` segfault the process (exit 139), not throw. Pure managed
  types (`Vector3`, `Color`, `Aabb`, `Transform3D`, `Mathf`, `Variant` over
  primitives) work fine. Never reference GodotSharp from this app.
- **Type schemas can be read without the engine.** `MetadataLoadContext` over
  the game assembly under `.godot/mono/temp/bin/Debug` loads cleanly and exposes `[Export]`
  hints, custom attributes with their constructor arguments, base types, and
  `ScriptPathAttribute`. Roughly 110 Resource derived types. Prefer consuming a
  committed schema manifest over reading the game's build output directly, so this
  app does not depend on the game being built.
- **UIDs can be read but should not be minted here.** `.godot/uid_cache.bin` is
  `u32 count` then per entry `{u64 id, u32 pathLen, utf8 path}`. The `uid://` text
  form is base 34 over the alphabet `a..y` then `0..8` (no `z`, no `9`). Godot mints
  UIDs itself on import, and `create_id_for_path` is seeded partly from the file's
  md5 so it is not stable across content edits.
- **Catalog addresses cannot be derived here.** `<project>:machine.moldurr` style
  addresses come from a native GDExtension (`addons/resource_catalog/bin/*.so`),
  and the `CritGG.ResourceCatalog` NuGet package is only a GodotSharp facade over it.
  Already resolved entries are readable from the committed
  `addon_data/resource_catalog/collections/*.tres`, but new files have no entry
  until the plugin runs.
