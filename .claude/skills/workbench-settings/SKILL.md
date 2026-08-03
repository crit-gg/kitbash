---
name: workbench-settings
description: "Workbench settings. Layered TOML scopes and layers, the write that preserves a file byte for byte, setting schemas and descriptors, the four schemas that exist, and per user application storage. Read before adding a setting or touching settings storage."
---

## Settings

TOML, layered, in `Workbench.Core/Settings`. Two independent axes:

- **Scope**: `Global` for the launcher and every tool, or `ForTool(id)`. These are
  separate namespaces, not a fallback chain. A missing tool setting does not
  resolve to a global value of the same name. Tools read both.
- **Layer**: `TeamShared` then `User`, lowest precedence first.

```
<workspace root>/.workbench/
  .gitignore          generated on first user write, ignores user/
  config/             team shared, committed to git
    workbench.toml      global scope
    tools/<id>.toml     tool scope
  user/               one person's overrides, never committed
    workbench.toml
    tools/<id>.toml
```

A workspace is any directory containing `.workbench`. `WorkspacePaths.Discover`
walks up to find it the way git finds `.git`, so a tool launched from a
subdirectory resolves the same settings the launcher does.

**Every workspace gets `config/workbench.toml` written the first time it is missing.**
`IWorkspaceScaffold` does it, from `WorkspaceRegistry.Resolve`, so it reaches a folder
cloned with a `.workbench` already in it and one added before this existed, not only a
folder added from here. A folder it cannot write to is survived rather than reported,
since a workspace works without the file.

**Every setting in it is commented out**, so nothing is set or overridden by it existing.
It is there to be read and edited, because until a settings window lands hand editing is
the only way to set any of this and an empty file answers no questions. **Every setting in
it is written from its descriptor**, so a key added to a workspace schema appears there
without anybody remembering. Only `workspace.name` is written by hand, since it has no
descriptor.

**The table headers are live and only the keys are commented.** Uncommenting one line then
puts the key in the table it belongs to, where under a commented header it would have made
a root key of the same name. An empty table sets nothing, so the file still changes no
value. This is why the scaffold writes `[godot]` and `# engine = ""` rather than the
`# godot.engine = ""` it wrote first.

A write keeps all of it. Saving `godot.engine` lands the value inside `[godot]`, under the
paragraph that describes it, and resetting it gives the file back exactly as it was.

Merging is per key, not per file. A user file holding one override does not hide
the rest of the shared config. Keys are dotted paths onto nested TOML tables, such
as `editor.font.size`. A value that exists but will not convert to the requested
type counts as absent and falls through to the layer below.

Reads go through `ISettings`. Writes go through `ISettingsService.Set`, which names
its layer explicitly because writing to `TeamShared` changes the setting for
everyone. `Apply` writes a batch to one file, so saving a page of edits is one read
and one write rather than one of each per key, and `Set` is one edit through it. A
batch that changed nothing writes nothing. There is no file watching, so one process
does not see another's write until it reloads.

`ISettingsDocumentStore` is the only place the file format lives. `SettingsDocument`
holds nested tables and no format specific types, so moving off TOML would touch one
class. It reads two ways: `Read` throws on a file that will not parse and `Open`
brings the failure back as a value, for a caller that draws it rather than fails.

### A write keeps the file

**`Apply` edits the file's text and leaves everything the edits did not name exactly as it
was.** Comments, blank lines, key order and the person's own spelling of a value all
survive, because the file is never rebuilt from the model. `TomlDocument` is that, holding
Tomlyn's `SyntaxParser` tree, which keeps all trivia and writes back byte for byte. It
carries the same three operations `SettingsDocument` has, so the store reads through one
and writes through the other.

`Write(path, document)` is still the model rewrite and still loses all of it. It has one
caller, `EngineStore`, whose file the app generates and nobody edits.

**A dotted key has five legal spellings and the search answers all of them**, since a
finder that misses one writes the key a second time somewhere else and leaves a file
holding the same key twice, which no later read can open. The five are `[a.b]` then `c`,
`[a]` then `b.c`, `a.b.c` at the root, `[a]` then `b = { c }`, and `a = { b = { c } }`.

**A key that is nowhere in the file takes the longest table that fits.** Then a new
`[a.b]` table at the end, and only a single segment key goes to the root, where TOML makes
it precede every table header. A new key goes after everything already in its table but
before the blank line and comment that introduce the next one.

**A comment above a key is not owned by that key and is left where it is.** The scaffolded
workspace file settles it: every setting there sits under a paragraph and a commented out
sample of itself, so taking the comment with the key would mean resetting a setting
deletes its documentation. An orphaned note is untidy, deleted prose is gone. The comment
after a value on the same line does go, since that one is part of the line. A table left
with nothing in it keeps its header for the same reason, which is where the file and
`SettingsDocument.RemoveValue` deliberately differ, since that one prunes.

**Trivia hangs off tokens, not off value or key nodes.** So replacing a value drops the
inline comment that followed the old one unless it is carried across by hand, and the
blank line before the next table trails the last pair's end of line token rather than
belonging to it. Both are handled in `TomlDocument` and both fail silently.

**The newline is the file's own, never this machine's.** `ToString` gives back what was
parsed, but trivia the code adds does not, so `TomlDocument` reads the file's first line
ending and writes new lines with it. A file that does not exist yet takes
`Environment.NewLine`. Following the file rather than the platform is what matters: a
Windows machine editing a CRLF file writes CRLF, and editing a colleague's LF file leaves
it on LF instead of putting one CRLF line in the middle of it.

**A byte order mark is put back.** Notepad and older Visual Studio write one, so a
workspace edited on Windows often has it, and `File.ReadAllText` takes it off the text it
returns while `File.WriteAllText` does not put it back. Writing the text straight out drops
it, which is a change to a committed file that the edit never named, and every collaborator
sees the encoding change along with the setting. So `Apply` looks at the first three bytes
and writes the mark back through a `StreamWriter` when it was there. A file without one
never gains one. Measured: 29 bytes in, 26 out, before this.

**Keys are compared ordinally, and that is not the same rule as paths.** A TOML key is case
sensitive on every platform, so nothing here goes through `IPathRules`, which exists
because Windows filesystems ignore case. `Godot.Engine` and `godot.engine` are two keys on
both platforms.

Measured on this machine, Linux, 119 checks over a scratch harness: a read and a write with
no edit byte for byte, every comment position surviving a change, an added key landing
inside its table and above the next table's comment, a new table appended, a single segment
key landing under the file header rather than above it, a reset giving a scaffolded file
back exactly, each of the five spellings set and removed with the result parsing and
holding the key once, a scalar replaced where a table belongs, arrays and floats and bools,
and a file with no trailing newline.

**The Windows cases were measured here too, and could be**, because line endings and
encoding are decided by .NET and by this code rather than by the OS. A file written the way
a Windows editor writes one, UTF-8 with a byte order mark and CRLF throughout, keeps its
mark, keeps every line ending, takes the edit, and comes back to the exact original bytes
when the setting is reset. What genuinely cannot be checked from here is the platform's own
behaviour underneath: `File.Move` replacing a file that another program holds open or that
carries the read only attribute fails on Windows where it would not here, which is what the
temporary file and the move were already doing before this.

**A file that will not parse is never written back.** This is the one hazard in the
whole system. Against a broken file the read produces an empty document, so a rebuild
would replace every hand written value with almost nothing under a button that said Save
changes. `Apply` refuses on the parse result rather than on the document being empty,
because after the fact those two look identical, and it refuses on the verdict `Open`
publishes rather than asking the syntax tree, so a write can never land on a file the
settings window has already drawn as unreadable. `SettingsFileUnreadableException` names
the file, and it is what `Read` throws as well.

### Schemas

A setting is described once, as an object, so a settings window can be built over any
app's settings without knowing anything about the app. `Workbench.Core/Settings/Schema`,
composed by `AddWorkbenchSettingsSchema`. The plan is `.claude/plans/settings-schema.md`.

**A descriptor is the one place a setting is defined**, and a reader takes its default
from it rather than passing one at the call site. `WindowSettingsSchema` is the first,
and `WindowSettings` is handed it rather than naming a key. **Hold a descriptor as an
instance member reached through a constructor.** A `public static readonly` one is the
obvious shortcut and it is ambient state.

Three levels: a `SettingsPage` is a tree node, it holds `SettingsSection`s under rule
headings, and a section holds rows. Almost every row is a `SettingDescriptor<T>`. The
other `ISettingsRow` implementation is the escape hatch an app supplies for a row that
is not a setting, such as a list of known workspaces or a button that resets the app,
so the schema never grows a way to describe a button.

**The scope belongs to the schema and the home belongs to the page.** Every page in the
launcher's window writes `workbench.toml` and every page in a tool's writes that tool's
file, so a tool cannot declare a global key by accident. `SettingsHome` is
`Application`, `Workspace` or `State`, and it decides which files stand behind a page
and whether there is a layer to choose. Only `Workspace` layers. There is no per setting
home and no per setting scope.

**A rule is data, never a predicate.** A `Func<T, bool>` can validate and nothing else,
so it cannot bound a spinner, write its own summary or be reasoned about. The set is
closed and adding to it is a deliberate change: `RangeRule<T>`, `LengthRule`,
`NotBlankRule`, `PatternRule`, `ChoiceRule<T>`, `PathShapeRule`.

**Rules, choices and probes are three different things.** `ChoiceRule` is a closed set,
so a value outside it is wrong and the layer below decides instead. `ISettingChoices<T>`
offers options discovered at runtime and never invalidates, because a pinned engine
version that is not installed is still the right value. `ISettingProbe` asks the
environment about a value that is already good, off the UI thread, and its answer
changes after the value is written.

**One message slot, two causes, and the origin says which.** A rule or conversion
failure means the value did not survive, so the origin is `Invalid` and the setting
falls back. A probe message means the value survived and the world is wrong, so the
origin stays the layer it came from. That is the whole of severity, and there is no
severity field.

**The editor is derived unless named.** A bool is a toggle, a closed choice is a segment
when it has at most three options of at most twelve characters and a dropdown otherwise,
a number is a number, an array is a list, and anything left is text. The two thresholds
are a guess rather than a measurement, so name a `SettingEditor` where they read wrong.

**`ISettingsInspector` is the read side, because `ISettings` merges and forgets.** It
opens every file behind a page and reports, per layer, what is stored and how it fared,
which layer won, whether reset would do anything, and whether each file is there and
readable. That closes three cases the merged read cannot: which layer won, that a lower
layer also holds a value, and that a stored value could not be used. Without it the
origin dot is decoration. It touches a disk, so it runs off the UI thread and caches
nothing.

**Reset removes the key, it does not write the default.** Writing the default pins
today's value and quietly opts that person out of every future change to it.
`SettingsEdit.Remove` is that, and it prunes a table it leaves empty.

**A home that is not composed is a home with nothing behind it.** `ISettingsHome` is one
per store, and `AddWorkbenchSettingsSchema` registers the two that need no workspace.
`AddWorkbenchSettings` contributes the workspace one, so a window composed without a
workspace reads its workspace pages as unavailable rather than failing on them. The
launcher changes which workspace is open at runtime, and nothing yet follows that, which
is the settings window's problem to solve when it lands.

`ISettingsWriter` saves a page. It refuses a read only page or home, a layer that does
not match whether the page layers, and any key the page does not declare, so a window
can only write what it drew.

### The schemas that exist

Four, all Core's, three in the `Application` home. That home is per user per machine and
has no layer, which is the point: **a path is right for one machine and wrong for every
other**, so none of those can be shared through a workspace's team config by accident.
`godot.engine` is the one that is genuinely a team fact, so it is the one that layers.

| Schema | Keys |
|---|---|
| `WindowSettingsSchema` | `window.nativeChrome` |
| `ExternalToolsSettingsSchema` | `tools.git.path`, `tools.dotnet.path` |
| `GodotSettingsSchema` | `godot.engines.directory`, `godot.engines.default`, `godot.build` |
| `WorkspaceGodotSettingsSchema` | `godot.engine`, and the only one in the `Workspace` home |

**A blank override means the app decides.** `tools.git.path` and `tools.dotnet.path`
default to blank, and blank is an answer rather than a gap, so `PathShapeRule` takes
`allowEmpty`. `IExternalTools` is what resolves it: the override when it points at
something runnable, and `IExecutableFinder` on PATH otherwise, which is what the app did
before the setting existed. `GitStatusReader` and `GitUpdater` both go through it, so the
duplicated PATH lookup they each held is gone.

**An override that points at nothing falls back rather than taking the program away.** A
bad override should not be worse than no override. It is reported instead, through
`ExternalTool.ConfiguredIsMissing`, so the choice is never silently ignored. Saying it is
a probe's job when probes land.

**Where there is a knowable default, the default is the real value, not blank.**
`godot.engines.directory` defaults to `ApplicationPaths.Engines`, worked out when the
schema is built, so a window shows the folder engines actually go in and resetting means
that folder rather than meaning nothing. A program found on PATH cannot do this, since
its answer is not known until something searches, which is why those two use blank.

`ApplicationPaths.Engines` is `engines` under the data directory, which is the case
`State` was put there for. It reads oddly on Windows, where the data directory already
ends in `State`, so engines land in `%LOCALAPPDATA%\Workbench\State\engines`. Moving it
means a fourth user directory and is a decision of its own.

`IExternalTools.Dotnet` is what `godot.build` set to Automatic resolves through, so a
machine with dotnet builds a project's C# with it and one without uses the editor.

`IExternalTools` resolves once and holds, so a change applies at the next launch, which
is what each description says and what the app already asked of someone installing git.

Measured on this machine, Linux: blank resolving to the git on PATH, an override actually
executed by `GitStatusReader` rather than PATH's git, an override pointing at nothing
falling back while reporting that it did, a relative path refused, blank accepted for a
tool and refused for the engine directory, and the engine default landing under the data
directory. The Windows half of `ExecutableFinder`, PATHEXT and the `.exe` name, is
unchanged by this and untested here.

Measured over a real workspace on this machine, 53 checks: layering and origin, a masked
team value, blank, out of range, unknown choice and wrong type all falling back with a
reason, reset removing and pruning, a batch landing, a broken team file drawn while its
write is refused and the file left byte for byte as it was, the personal layer still
writable beside it, and the read only and undeclared key guards.

## Application storage

What Workbench keeps for one person on one machine, outside any workspace, so it can
be read before a workspace is known. Three places, because they are backed up, roamed
and cleared differently. `IUserDirectories` says where each one is and is the only
thing that knows the OS layout. `ApplicationPaths` names the files under them.

| | Linux | Windows |
|---|---|---|
| Configuration | `$XDG_CONFIG_HOME/workbench` or `~/.config/workbench` | `%APPDATA%\Workbench`, roams |
| State | `$XDG_DATA_HOME/workbench` or `~/.local/share/workbench` | `%LOCALAPPDATA%\Workbench\State` |
| Cache | `$XDG_CACHE_HOME/workbench` or `~/.cache/workbench` | `%LOCALAPPDATA%\Workbench\Cache` |

An XDG variable holding a relative path is ignored, which the spec requires.

The application folder is lower case on Unix and keeps its written case on Windows,
which is what each platform does with its own directories. `IUserDirectories` folds it,
so a caller passes the application name once and never thinks about case.

State sits in the data directory by choice. The spec would put it under
`$XDG_STATE_HOME`, since a workspace list is a recently used list. It is here instead,
so a later directory for real user data would share this folder rather than take a
fourth one. Do not move it back without asking.

**`IApplicationSettings`** is configuration. Choices a person may edit by hand. It has
no layer to choose, so these can never be shared through a workspace's team config.

Typed readers sit over it rather than call sites naming keys. `IWindowSettings` is the
first, and it carries `window.nativeChrome`, covered by the `workbench-windows` skill.

**`IApplicationState`** is what the app remembers for itself. The list of workspaces,
which one is open, and window geometry when that arrives. The app writes it, a person
does not. Deleting the state directory resets Workbench without touching anything
anyone chose, which is the whole reason it is not in the config file.

Both take the same shape, scopes and all, and both are TOML through the same
`ISettingsDocumentStore`. They share `ScopedDocuments` and differ only in which
directory their files land in. `ISettings` is the read side of both, so read its name
as a typed read over a document rather than as a claim about settings.

The cache has a directory and `ApplicationPaths.CacheFileFor`, and no API beyond that.
Nothing caches anything yet, so the first thing that does picks its own shape.

