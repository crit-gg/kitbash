# Kitbash

Cross platform desktop app hosting designer facing tools for the Slopworks Godot
project at `/home/jason/Projects/godot/slopworks/godot`.

The point of this app existing outside Godot: the Godot editor's inspector and
save/load behavior are awkward for authoring gameplay data, and designers should
be able to work without the editor open.

## Cross platform is a requirement, not a goal

Every change is written for Windows and for Linux, and on Linux for any distribution
and any desktop. Development happens on one machine, so the other cases are never
the ones being looked at. They are still the ones that break.

Before writing anything that touches the filesystem, the environment, a path, a
process, a window or a user visible directory, answer three questions:

1. **What does the other OS do here?** If the answer differs, it goes behind an
   interface with one implementation per OS, chosen by a factory in
   `KitbashCoreServices`. That file is the only place allowed to test the running
   OS. Never branch on the OS at a call site, and never assume a separator, a
   directory layout, a case sensitive filesystem or a shell.
2. **What does the other distribution do here?** Assume no particular distribution,
   package manager, init system, desktop or file manager. Read the standard, honor
   the environment variable, and probe for what is installed rather than naming one
   thing. `DesktopLauncherResolver` is the shape to copy: a list of candidates, the
   first one present wins, and a message that lists what was tried when none are.
3. **What happens when it is not there?** A variable can be unset, empty, relative or
   nonsense. A directory can be missing. A program can be absent. Each of those has a
   defined answer, and the answer is never a crash.

Sandboxes count as distributions. Flatpak and Snap move every user directory, and
they say so through the standard variables, so code that reads the variable works and
code that hardcodes a path does not.

State the assumption in a comment when behavior is pinned to something external, such
as a spec rule or a variable a platform always sets. Verify what can be verified here,
and say plainly what could not be tested because this machine is Linux.

## Comment and documentation style

Applies to code comments, XML docs, markdown, and anything else written as prose.

- No semicolons.
- No hyphenated words. Write "read only", not the hyphenated form. Identifiers,
  paths, and package names keep their real spelling.
- No em dashes.
- Keep language simple. Short sentences.
- Write for maintenance. Say what a reader needs in order to change the code safely.

### A comment is short and it is for the next developer

**Two lines is normal and four is the limit.** Anything longer is not a comment, it
is documentation, and it belongs in `.claude/`. A file is read hundreds of times and
a rationale is needed once.

**No `<remarks>` blocks. No `<para>` tags.** A `<summary>` of one or two lines is the
whole XML doc, and a summary that wants a paragraph break has already outgrown the
file it is in. `<param>`, `<returns>` and `<exception>` are fine, one line each. A
short `<example>` is fine on a public type somebody has to call from outside.

**Never write the conversation into the code.** No account of what was tried, what
was measured, what a design page says, what was decided, or why one approach beat
another. None of that helps somebody change the code, and all of it goes stale the
moment the code moves. The code says what it does. `.claude/` says why.

Phrases that mean a comment should not exist, or should be one plain sentence
instead: "Measured", "The design draws", "This is deliberate", "was tried", "on
purpose", "the whole reason", "which is why", "rather than", "so a person".

**Say the thing the code cannot say.** That is the only test worth applying:

- a unit, a range, or an encoding
- an obligation on the caller, such as a thread or an order
- an external behaviour being relied on, named plainly, such as a spec rule or a
  variable a platform always sets
- a workaround for a framework bug, with what breaks if it is removed

If none of those apply, write no comment and let the names carry it.

```csharp
// Godot writes this only when a person closes the editor, so it is often absent.
private string? ReadLayout(string file) => ...

// Wrong. This is documentation wearing a comment's clothes.
/// <remarks>
/// **No layout file is the ordinary case, not a problem.** Godot writes it when a
/// person closes the editor, so a project that has only ever been imported has
/// none, and a fresh clone has none either. Measured on the probe project ...
/// </remarks>
```

Markdown in `.claude/` is where the long form lives, and it has no length limit.
That is the trade. Move prose there rather than deleting knowledge.
## User facing copy

Applies to every string a person reads in the app. Labels, buttons, tooltips,
menu items, headings, placeholders, status text, error messages, dialogs.

- No em dashes.
- No semicolons. Split the sentence instead.
- Avoid hyphenated words. Reword rather than hyphenate. Proper names and product
  names keep their real spelling.
- Keep it short. A label is a few words, a message is a sentence.
- No special characters. No middle dots, arrows, ellipsis characters, bullets or
  anything else decorative. Ordinary letters, digits and plain punctuation only,
  unless asked for one directly.

## Code

No static classes, so collaborators stay replaceable. Two exceptions:

- A container for extension methods must be static. There are two, one per library:
  `KitbashCoreServices` and `KitbashUiServices`.
- Static factory methods on an instance type are fine, such as `WebAddress.Parse`
  and `ToolActivationResult.Failure`.

Dependencies arrive through the constructor. Nothing builds a collaborator inside a
method, and nothing reaches for ambient state. Filesystem access goes through
`IFileSystem` and environment variables through `IEnvironment`.

## Composition

Microsoft.Extensions.DependencyInjection. Each executable is its own composition root
and builds one provider at startup. `App.BuildServices` is the launcher's.

Core exposes registration methods rather than a container of its own:

- `AddKitbashIO` filesystem, environment, user directories, path display
- `AddKitbashPlatform` the services that differ per OS
- `AddKitbashApplicationStorage` settings, state and the cache for this machine
- `AddKitbashWorkspaces` the list of workspaces a person has added
- `AddKitbashWorkspace` workspace discovery
- `AddKitbashSettings(paths)` settings for one workspace

`Kitbash.Ui` exposes one of its own, `KitbashUiServices`:

- `AddKitbashToasts` the toast service, its clock and its settings
- `AddKitbashSettingsWindow` the settings window, over whatever schema is registered

They use `TryAdd`, so calling several is safe and a caller can substitute any service
by registering its own first.

`Kitbash.Ui` takes `Microsoft.Extensions.DependencyInjection.Abstractions` for that, so
the library asks for the contract and the application still picks the container.
`Kitbash.Core` already takes the same package. It also takes `CommunityToolkit.Mvvm`,
which the settings window's view models are the only user of.

## Commits

Commits have one author, the user. Never add a coauthor trailer and never list the
agent as an author.

## Layout

```
Kitbash.slnx
src/Kitbash.Core/    shared contract (ITool, IToolActivation, IToolRegistry)
src/Kitbash.Ui/      the look: tokens, type, control themes, fonts, the window shell
src/Kitbash.Gallery/ every control, live, for building and checking the library
src/Kitbash/         the launcher app, Avalonia 12
src/tools/           one project per tool, empty until the first tool is named
```

**Kitbash** is the launcher and is its own executable. `Kitbash` is also the
project name prefix for anything above the tool level. Tools are separately named
products and are not prefixed.

**`Kitbash.Ui`** is the look, shared by the launcher and every tool. It references
Avalonia and `Kitbash.Core`, and the reference only ever points that way, so the
contract stays free of a UI framework. A consumer takes one line:

```xml
<StyleInclude Source="avares://Kitbash.Ui/Themes/KitbashTheme.axaml" />
```

That brings the tokens, the type scale, the icons and the window shell. Nothing outside
`Themes/Tokens.axaml` writes a colour, a size or a radius. Names say what a value is for
rather than what it looks like, and two names may share a value when they are genuinely
different roles, which is noted in the file where it happens.

Everything a consumer names lives in one namespace, `Kitbash.Ui.Controls`, so a view
declares one xmlns:

```xml
xmlns:ui="clr-namespace:Kitbash.Ui.Controls;assembly=Kitbash.Ui"
```

## Where the rest of the rules are

This file holds only what governs every change. Everything else is a skill under
`.claude/skills/`, loaded when the work touches it, so a session does not carry the
whole rulebook to fix a typo.

**Read the skill before changing anything in its area.** Each one is the rules for a
part of the app, and each is the only place those rules are written.

| Skill | Read it before |
|---|---|
| `kitbash-controls` | any `ControlTheme`, button, input, icon or the status bar |
| `kitbash-surfaces` | panels, the depth ramp, lists, trees, tabs, overlays |
| `kitbash-toasts` | raising a toast or drawing an alert |
| `kitbash-windows` | a window or dialog, or work that touches a disk on the UI thread |
| `kitbash-settings` | a setting, a schema, the settings window, or per user storage |
| `kitbash-platform` | the filesystem, the environment, a path or a process |
| `kitbash-godot` | engine matching or launching a project |
| `kitbash-git` | anything that runs git |

Deeper reference, read when a skill sends you there or when the framework itself is
the problem:

- `.claude/avalonia.md` Avalonia 12 behaviour, measured here. Most material online
  still describes 11. Read it before working on views, styling or window chrome.
- `.claude/dotnet.md` the .NET and toolchain notes
- `.claude/godot-engines.md` how engine builds are named, downloaded and installed
- `.claude/plans/` the twelve design stages and the plans not yet started

**A new rule goes in the skill it belongs to, not here.** This file grows only when a
rule applies to every file in the repository. If a skill has no home for it, add a
skill rather than a section here.
## Commands

```
dotnet build
dotnet run --project src/Kitbash
```

A runtime identifier cannot be passed to the solution, only to a project.

```
dotnet publish src/Kitbash/Kitbash.csproj -r win-x64 --self-contained
dotnet publish src/Kitbash/Kitbash.csproj -r linux-x64 --self-contained
```

Releasing is `build/release.sh <feed directory> [linux|win]`, which does both of those and
packs each into a Velopack feed. It needs `dotnet tool install -g vpk`. `vpk` cross
compiles between Windows and Linux, so one machine builds both, and the version comes from
`<Version>` in `Directory.Build.props` rather than being typed again.

Set `updates.feed` to that directory to watch a real update happen. A copy started with
`dotnet run` never updates itself, whatever the feed says.

## Stack

- .NET 10, Avalonia 12.1.1, CommunityToolkit.Mvvm 8.4.2, Tomlyn 2.10.1, Velopack 1.2.0,
  Humanizer.Core 3.0.10, Microsoft.Extensions.DependencyInjection 10.0.10
- Avalonia 12 changed a lot from 11 and most material online still describes 11.
  Read `.claude/avalonia.md` before working on views, styling or window chrome.
- Velopack is the launcher's alone. Neither library takes it, because a tool never checks
  for its own update. `VelopackApp.Build().Run()` has to stay the first statement in
  `Main`, since Velopack reruns the binary with hook arguments and exits from inside it.
- Tomlyn 2.10 is a redesign. The old `Toml` static class is gone, replaced by
  `TomlSerializer` with a `System.Text.Json` style API.
- Humanizer writes the English a person reads: plurals that agree with a count, and a
  timestamp as how long ago it was. **It is `Humanizer.Core`, not `Humanizer`.** The meta
  package carries a satellite assembly per language, and nothing here is translated.
  `Kitbash.Core` does not reference it, so it goes in each executable that needs it and
  the contract stays on Tomlyn alone.

## Status

Scaffolding, on the Slate design. The launcher lists the three placeholder tools the
registry holds and opening one reports that it is not built. No tool is implemented, and
no file format or Godot integration work has started.

**The launcher can install and update itself, and updates are off.** Velopack packages it,
`build/release.sh` publishes both runtimes and packs both from one machine, and at start
the app checks its feed and replaces itself over a progress dialog before drawing anything.
All of that is built and measured. **`UpdateSettingsSchema.IsEnabled` is false**, because
there is nowhere to publish to yet, so nothing checks and the settings window offers no
feed. Releases will go to Backblaze B2 from a GitHub workflow that versions them itself.

The pack id is `Slopworks.Kitbash` and it must stay namespaced, because Velopack's
uninstaller deletes all of `%LocalAppData%\{packId}` and `Kitbash` alone would put a
person's state and engines inside it. `.claude/plans/distribution-and-updates.md` has the
rules and records five things it originally got wrong. **Tool distribution is not built**,
so `ITool` and the registry are unchanged.

The app is being moved to the Slate design, in the twelve stages under
`.claude/plans/`. **The numbers are the order**, and every stage depends only on lower
ones, so the plan runs straight through.

Stages 1 to 11 are done. `Kitbash.Ui` carries the Slate tokens, the type scale, the 49
icons, the window shell, the activity rail, every overlay surface, the depth ramp, the
settings window, and the control themes built so far: five button kinds, the split
button, the dropdown button, the chip, the badge, the status pill, the progress bar, the
panel, the expander, the splitter, the collapsing sidebar, the text fields, the search
field, the path field, the checkbox, the radio, the toggle, the segmented row, the slider, the spinbox, the
combo box, the hyperlink, the list row, the tree, the tabs, the toast, the alert, both data
grids and the pager.

Stage 8 is done except the colour field, which waits on stage 13 because its swatch has
nothing to open until the picker exists.

**`ui:PathField` is built**, from the `Theme Slate - Path Field` design, which is outside
the twelve stages. It holds one path or none, browses for a file or a folder, filters both
the dialog and a typed path, takes a drop, and is what the settings window draws for every
setting carrying a `PathShapeRule`. The clone dialog uses it too.

**Both grids are built and they share one column model.** `ui:DataGrid` is a `ListBox` and
`ui:TreeDataGrid` is a `ui:Tree`, so neither writes virtualisation, selection or the row
states again. Sorting, grouping, inline edit, column resizing and the sideways header are all
real, and paging is `ui:GridPager`, a separate control, so a grid that never pages carries
none of it. The `kitbash-surfaces` skill has the rules.

The launcher is Slate throughout and holds no brush, hex, font size or radius of its own.
It is a shell now, a title bar over a rail and a page, carrying the workspace page, the
Godot engines page and a settings window.

**The launcher holds a `ToastHost`**, wired in `LauncherWindow` to the engines page's
`IToastService`, since installing an engine is the first thing it had worth reporting.
The one slow thing it does over a workspace, updating from git, still reports on itself in
the status bar in place, which is rule 8 of that row and not something a toast should take
over. The gallery is still where the toast service is exercised in full.

The git strip and the engine strip are both real and both read the open workspace.

**The workspace popover is finished.** Each row's menu opens a folder, copies its path,
renames it and takes it off the list, and the footer adds a workspace from a folder or
clones one from git. The clone dialog runs `IGitCloner` and stays open until git has
finished, so what it hands back is a folder that is really there. Renaming and cloning are
covered by the `kitbash-platform` and `kitbash-git` skills.

**The tools section is drawn and its data is invented.** The set of tools is the registry's,
and every version, update, install state and blocked state on a card comes from
`Kitbash/Mock/MockToolCatalogue.cs`. Update, Update all and Check for updates run timers
and download nothing. That stands until there is an answer to where a tool comes from, which
is an open decision below. Delete the mock when there is.

**The settings window is built and it is the launcher's.** Steps 1 to 5 of the six in
`.claude/plans/settings-schema.md` are done and only probes are left. `SettingsWindow` is in
`Kitbash.Ui` and draws whatever `SettingsSchema` it is handed, so a tool writes a schema
class and one line to open it. The launcher's schema is
`Kitbash/Settings/LauncherSettingsSchema`, its tree is Application, Workspace and State,
and the rail's cog opens it. The Workspace store lists every workspace by name with its own
settings under it, so any of them can be changed without switching to it first. Nothing has to be hand edited any more: every setting Core
declares is drawn, `workspaces.directory` included. A setting nothing rereads carries
`NeedsRestart`, and the footer grows a Save and restart button while one is waiting.

The design's red Reset Workbench button is deliberately not built. It deletes a person's
whole workspace list and nothing asked for it, so the State page reads the registry and
changes nothing.

The rail carries every state, including the two a pointer cannot reach on its own, an open
page under the cursor and an item that is open but disabled. All four are in the gallery.
The cog at its foot opens a window rather than a page, so it never stays selected.

## Open decisions

Do not assume any of these. Ask before building on one.

- Whether designer data moves to a custom format at all, and what that format is
- How the Godot side consumes it (EditorImportPlugin was the leading candidate,
  a runtime ResourceFormatLoader was the alternative)
- How references between data files are expressed (source file path was the
  leading candidate)
- Where a tool comes from. Compiled into the launcher as now, found on disk under a known
  directory, or fetched from a remote index. The tools page draws all three states and
  `ITool` answers none of them, so this decides what `src/tools/` builds into as well

