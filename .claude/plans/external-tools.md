# Open in external tool

The icon button right of the Open in Editor split button. It offers every editor, IDE and
terminal found on this machine, plus the ones a person added, and opens the workspace
folder or a solution inside it.

Ported from SourceGit at `/home/jason/Projects/sourcegit-master`, which is MIT licensed.
The rules that govern it are in the `kitbash-platform` skill. This file is the record of
what was taken, what was changed and what could not be checked.

## Where each piece came from

| Kitbash | SourceGit |
|---|---|
| `Platform/Openers/WorkspaceOpener` | `Models/ExternalTool` |
| `Platform/Openers/WorkspaceOpeners` | `Models/ExternalToolsFinder` plus `ExternalTool.Launch` |
| `Platform/Openers/WorkspaceFileFinder` | `App.Extensions.cs DirectoryInfoExtension.WalkFiles` |
| `Platform/Openers/JetBrainsToolbox` | `ExternalToolsFinder.FindJetBrainsFromToolbox` |
| `Platform/Windows/WindowsWorkspaceOpenerFinder` | `Native/Windows.cs FindExternalTools` |
| `Platform/Linux/LinuxWorkspaceOpenerFinder` | `Native/Linux.cs FindExternalTools` |
| the terminal candidate lists | `Models/ShellOrTerminal` |
| the menu itself | `Views/RepositoryToolbar.axaml.cs OpenWithExternalTools` |
| the brand PNGs | `Resources/Images/ExternalToolIcons` and `ShellIcons` |

The registry GUIDs, the `vswhere` arguments, the JetBrains product code list and the two
walk depths, 2 for `.code-workspace` and 4 for `.sln`, are all copied verbatim.

## What was changed, and why

**`ExternalTool` was not reused.** That name is taken in Core and means where git and
dotnet are. The new concept is `WorkspaceOpener`.

**Arguments are a list, not a string.** `ProcessRequest.Arguments` is
`IReadOnlyList<string>`, so nothing quotes anything. SourceGit passes one string and calls
`.Quoted()` at every site.

**A custom tool can be a new tool.** SourceGit's `external_editors.json` only re points or
hides a tool it already knows, with no name and no arguments. Here `tools.custom` is an
array of tables in the global config with a name, a path and an argument template, edited
on the Open in settings page. There is no exclude list, since a tool nobody wants can be
left alone.

**A terminal is offered by name.** SourceGit makes the terminal a preference and opens the
one that was chosen. Every terminal found is a row here.

**Detection is a service, not a static.** SourceGit finds once at startup into
`Native.OS.ExternalTools`. Both are once per process, and the difference is that a custom
tool here is read every time, so adding one in the settings window shows up without a
restart.

**JetBrains entries are checked.** SourceGit's Toolbox branch bypasses its own
`File.Exists`, so a removed IDE stays in the menu. This checks the program first.

**Sublime Text and Zed were not ported.** They were not asked for. Adding either is a
candidate in each finder and a PNG, nothing more.

**Cursor opens a `.code-workspace`.** SourceGit gives it no launch options. It is a VS Code
fork and does open them.

**Rider opens a solution.** SourceGit gives every Toolbox IDE the folder and nothing else.
Rider is the .NET one, so `RD` alone gets the `.sln` and `.slnx` walk. A solution means
nothing to WebStorm or DataGrip and they still open the folder.

**Visual Studio falls back to the folder.** SourceGit sets `supportOpenFolder: false` for
it, which hides Visual Studio entirely from a workspace that has no solution yet. Here every
tool opens the folder, so `OpensFolder` was removed rather than left as a flag nothing sets.

**There are no submenus.** SourceGit folds a tool that found several things into a submenu
with `Open as Folder` under a rule. The menu here is one flat list: a row opens the first
solution found, or the folder. A workspace holding more than one solution is not offered the
choice, and the `Open as folder` row went with the submenu that held it.

## What is not built

- **The `~/.local/bin` fallback is in the Linux finder, not in `IExecutableFinder`.**
  Moving it up would quietly change how git and dotnet resolve.
- **No macOS.** Kitbash is Windows and Linux.
- **No GitHub style per workspace tool list.** `tools.custom` is global only, the same
  shape `tools.repositories` started as.

## Two traps found while building it

**A `MenuFlyout` filled from its own `Opening` handler opens empty.** The presenter is
already built by the time that fires. The fill moved onto `Rows` changing instead, which is
also earlier and touches nothing on the way open. This cost a debugging pass, so it is
written down in the `kitbash-platform` skill as well.

**`Classes="icon"` is 24, and a control is 26.** The kind's box is a hit area rather than a
control height, so a bare icon button beside a `SplitButton` sits a little short. The button
pins `HeightControl` on both axes to stay square and match the row.

## What was measured here

This machine is Linux. Detection was run against it and found Visual Studio Code, six
JetBrains IDEs through Toolbox and Konsole, every one of them naming a runnable program.
The Toolbox entries all carry an absolute `launchCommand`, which is the branch that gets
exercised.

Tests: `tests/Kitbash.Core.Tests/Openers` covers the argument template, the bounded walk
and the `tools.custom` round trip against a real config file. `tests/Kitbash.Tests` is a
new project, headless with real drawing, covering the menu controls, every brand mark
resolving, the row rules and the button's place in the engine strip grid.

Both runtimes publish from this machine with `TreatWarningsAsErrors` on, and
`Microsoft.Win32.Registry.dll` is in both outputs without a package being named.

## What could not be checked

**The whole of `WindowsWorkspaceOpenerFinder`.** Every registry key and its `DisplayIcon`
value, the `vswhere` output shape and its UTF 8 encoding, the Cursor install path, the Git
Bash derivation, and whether `StartDetached` honours `WorkingDirectory` through the shell.
The relative `launchCommand` branch in `JetBrainsToolbox` is Windows only and is reasoned
too.

That is the same footing `WindowsSecretStore` is on, and it is recorded here rather than
left implied.
