# Opening a project.godot

Double clicking a `project.godot`, or choosing Kitbash in Open with, opens that project.

## What opening does

The user decided all four of these.

1. **A workspace's own project goes the way that workspace does.** Known means the
   `project.godot` that `IGodotProjectReader.Find` answers for a workspace's root, compared
   through `IPathRules`. It goes through `LauncherViewModel.LaunchFromLinkAsync` in editor
   mode, so it switches to the workspace and builds, imports and starts exactly like the
   `kitbash://workspace/<name>/edit` link, including starting nothing on an engine that does
   not match.
2. **Any other project is started with nothing first.** No workspace is added. The version
   and the C# flag come off the file alone, the resolver is asked with no default, and only a
   `Matched` engine opens it, through `IGodotLauncher.StartEditor`, which is
   `--path <folder> --editor` detached. No build, no import, no dialog. Anything else is a
   toast naming what is missing. The launcher's after editor setting still applies.
3. **A `project.godot` inside a workspace that is not the workspace's own project** is the
   second case.
4. **Kitbash is always in Open with, and the default only when asked.** That is
   `launcher.projectFiles.default` on the Launcher page. Nothing needs elevation on any OS.

`IProjectFileRouter` in Core makes the decision and `ProjectFileRoute` carries it, so the
rules are tested over real files with no window.

## How the file arrives

The same way a link does. Windows and Linux start a second copy with the path as an argument,
and `ISingleInstance` carries it to the running copy. The desktop entry's `%u` may hand over a
`file:` url instead of a path, which `ProjectFileArgument` takes. macOS raises a
`FileActivatedEventArgs`. `App._pending` holds either a link or a path, and `Follow` tells
them apart.

**A project file is deliberately not a `kitbash://` verb.** A web page can carry a link, and
opening a project in the editor runs its tool scripts, so a path never travels as one.

The running launcher comes to the front for a file as it does for a link, since the toasts
that report a problem live there.

## Registration

The `kitbash-platform` skill has the per OS rules. In short:

| OS | Offered | Default |
|---|---|---|
| Linux | `MimeType` in the entry, the type defined under `$XDG_DATA_HOME/mime` | both sections of `mimeapps.list` |
| Windows | `Kitbash.GodotProject` under `.godot\OpenWithProgids` | `.godot` default value |
| macOS | `CFBundleDocumentTypes` at `Alternate` over `public.godot` | not built |

Turning the default off gives back only what names Kitbash. Every other entry stays.

## What was verified, and where

| Verified | How |
|---|---|
| the argument parser | `ProjectFileArgumentTests` |
| the route for every case | `ProjectFileRouterTests`, over real project files and a fake engine store |
| the entry, the definition, claiming and giving back | `DesktopIntegrationTests`, over a real data root |
| GLib reads the definition once the database is built | `update-mime-database` and `gio info` over a scratch data root on this Linux machine, which has no Godot package: `text/plain` before, `application/x-godot-project` after |
| the generated plist parses and carries both blocks | the heredoc rendered and read with Python's `plistlib` |

**Not run:** any of the Windows registry writes or the Explorer refresh, the macOS document
type and file activation, and a real double click in a file manager on any desktop.

## What is not built

- **macOS cannot be made the default.** It needs `NSWorkspace` or LaunchServices interop,
  and the setting does nothing there.
- **An unknown project matches official builds only.** A project built on a team's fork that
  is not a workspace finds no engine, since a repository is named only in a workspace config.
- **Offering on GNOME does not list Kitbash first.** With no association line written, GLib
  lists Kitbash only under all applications, until a person picks it once.
