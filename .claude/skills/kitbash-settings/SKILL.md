---
name: kitbash-settings
description: "Kitbash settings. Layered TOML scopes and layers, the write that preserves a file byte for byte, setting schemas and descriptors, the schemas that exist, the settings window every app shares, and per user application storage. Read before adding a setting, touching settings storage, or changing the settings window."
---

## Settings

TOML, layered, in `Kitbash.Core/Settings`. Two independent axes:

- **Scope**: `Global` for the launcher and every tool, or `ForTool(id)`. These are
  separate namespaces, not a fallback chain. A missing tool setting does not
  resolve to a global value of the same name. Tools read both.
- **Layer**: `TeamShared` then `User`, lowest precedence first.

```
<workspace root>/.kitbash/
  .gitignore          generated on first user write, ignores user/
  config/             team shared, committed to git
    kitbash.toml        global scope
    tools/<id>.toml     tool scope
  user/               one person's overrides, never committed
    kitbash.toml
    tools/<id>.toml
```

A workspace is any directory containing `.kitbash`. `WorkspacePaths.Discover`
walks up to find it the way git finds `.git`, so a tool launched from a
subdirectory resolves the same settings the launcher does.

**Every workspace gets `config/kitbash.toml` written the first time it is missing.**
`IWorkspaceScaffold` does it, from `WorkspaceRegistry.Resolve`, so it reaches a folder
cloned with a `.kitbash` already in it and one added before this existed, not only a
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

**A value that is a list of dictionaries is an array of tables**, and it is written as
`[[a.b]]` blocks rather than as one long line. Rows are matched to the blocks already in
the file by position, so a row that did not change keeps its own comments and key order,
a surplus block is removed and a new row is appended. A file already spelling it inline,
as `a.b = [{ }]`, keeps that spelling, since writing blocks beside it would leave the key
in the file twice and no later read could open it. `tools.repositories` is the only such
key and `.claude/plans/tool-distribution.md` has what it holds.

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

**An array of tables is read and never written.** `[[a.b]]` reads back as an array of
dictionaries, which is what the tool repository list is. Tomlyn has two array types and a
table array is not a `TomlArray`, so it takes its own case in the store. Nothing writes
one: `ToTomlValue` would make a plain array of inline tables, and no page declares such a
key, so a write is refused before it gets there. A write into a file holding one leaves it
exactly as it was, which was measured.

**A key added to a table that holds only comments lands under them, after the blank line
that separates the next table.** Valid TOML, read back correctly, and a reset gives the
file back byte for byte, but the blank line ends up above the new key rather than below
it. `AddTo` carries trivia off the last pair's end of line token and an empty table has no
pair to carry from. Cosmetic, and untouched.

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

The array of tables was measured the same way, 26 checks: a new key written as blocks, two
rows written as two blocks, an edited row keeping its own comment while the tables around
it stay untouched, a surplus block removed, an empty list taking every block out, the
inline spelling staying inline and the key written once, an unchanged list coming back byte
for byte, a key the row does not name dropped, a plain setting written into a file holding
a list leaving the list alone, and a CRLF file with a byte order mark keeping both.

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
app's settings without knowing anything about the app. `Kitbash.Core/Settings/Schema`,
composed by `AddKitbashSettingsSchema`. The plan is `.claude/plans/settings-schema.md`.

**A descriptor is the one place a setting is defined**, and a reader takes its default
from it rather than passing one at the call site. `WindowSettingsSchema` is the first,
and `WindowSettings` is handed it rather than naming a key. **Hold a descriptor as an
instance member reached through a constructor.** A `public static readonly` one is the
obvious shortcut and it is ambient state.

Three levels: a `SettingsPage` is a tree node, it holds `SettingsSection`s under rule
headings, and a section holds rows. Almost every row is a `SettingDescriptor<T>`. The
other two `ISettingsRow` implementations are the escape hatch an app supplies for a row
that is not a setting, such as a list of known workspaces or a value the schema cannot
describe. Neither has a key, so the schema never grows a way to describe a button.

**The scope belongs to the schema and the home belongs to the page.** Every page in the
launcher's window writes `kitbash.toml` and every page in a tool's writes that tool's
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

**A setting nothing rereads carries `NeedsRestart`.** `window.nativeChrome` and the three
external tool paths are the four, since a window keeps the frame it opened with and
`IExternalTools` resolves once and holds. It is a field rather than a sentence in the
description, because the window offers to restart on the strength of it. Do not write
"takes effect at the next launch" in a description as well.

**The editor is derived unless named.** A bool is a toggle, a closed choice is a segment
when it has at most three options of at most twelve characters and a dropdown otherwise, a
setting carrying a `PathShapeRule` is a path field, a number is a number, an array is a
list, and anything left is text. The two thresholds are a guess rather than a measurement,
so name a `SettingEditor` where they read wrong.

**A choice beats a path.** A closed set is still a set when its values happen to be paths,
so `ChoiceRule` is tested first and a descriptor carrying both draws a segment or a
dropdown.

**A path setting is drawn by `ui:PathField`, and the rule's `PathKind` picks the dialog.**
`Directory` browses for a folder and both other kinds browse for a file, since a dialog is
one or the other and `Any` has to choose. **No filters are passed.** A rule says the shape
of a path and never its extensions, and the only files these point at are programs, which
carry `.exe` on Windows and no extension on Linux. Whether the path is really there is a
probe's answer, not the control's.

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
per store, and `AddKitbashSettingsSchema` registers the two that need no workspace.
There are two ways to get the third:

- `AddKitbashSettings(paths)` for an app that opens one workspace and keeps it. Writes
  through `ISettingsService`, so that service's cache is dropped by the same call.
- `IWorkspaceSettingsFactory`, from `AddKitbashWorkspaces`, for reading a workspace named
  at the call rather than one held for the life of the app. The launcher reads the open
  workspace's repository list through it.
- `AddKitbashKnownWorkspaceSettings()` for an app that holds a list of them, which is
  the launcher. Writes straight to the document store, since nothing in that app caches a
  workspace's settings.

**A home has places, and a page is read for one of them.** `ISettingsHome.Places` is what
the home keeps files for. A home over this machine has a single place with no name. The
launcher's workspace home has one per workspace a person has added, each named the way the
launcher names it, so its window lists them all rather than following whichever one is
open. `ISettingsInspector.Read` and `ISettingsWriter.Write` both take the place, and null
means the only one.

**Only workspaces that are still on disk become places.** A workspace whose folder has
gone has nothing to change, and offering to write into a folder that is not there would
only fail. It stays in the launcher's list and in the State page either way.

**Places are asked for again every time, and compared by id.** A person can add a
workspace while the window is open. The id is opaque and only ever names a place the home
itself listed, so it is compared ordinally rather than through `IPathRules`. A home with
no places at all reads a page as `IsAvailable: false` with no files, which is what the
window draws its empty state from.

`ISettingsWriter` saves a page. It refuses a read only page or home, a home with nowhere
to be, a layer that does not match whether the page layers, and any key the page does not
declare, so a window can only write what it drew.

**A row that is not a setting is a `SettingsReadoutRow` or a `SettingsEditorRow`.** One
reads and one edits, and those two are the whole escape hatch. A readout is a name, an
optional description, a style and a function returning lines. The function is called again
every time the page loads, since what it says changes while the app runs.

**The style is the presentation and it is never derived**, because the answer must not
change with the data. A list that happened to hold one line would otherwise redraw itself
as a value.

| `SettingsReadoutStyle` | Drawn as | For |
|---|---|---|
| `Value`, the default | plain mono text where an editor would be | one fact, such as `Current version` |
| `List` | a well of lines, each with a mark | a set, such as the known workspaces |

**A description is optional here and required on a setting.** A fact whose name says the
whole of it needs no sentence under it, and the window leaves the line out rather than
drawing it empty, since an empty one still takes its height. `Current version` carries
none.

**Every line is copyable, by right click.** The line carries `Background="Transparent"`,
without which it is not hit tested and the click never lands. The menu sits inside the
line's own template, so its data context is the line.

**A row that shortens for the display must say what it shortened.** `SettingsListEntry`
takes a `Full`, and `Copied` hands that back when there is one. Otherwise a workspace row,
which draws a path through `IPathShortener`, would put an elided path on the clipboard,
which reads fine and pastes as nothing anybody can use. The same lines are what an array
valued setting draws, so those are copyable too.

**A readout can sit on a page that also has settings**, as its own section above them. The
Updates page is the shape to copy: a `This copy` section holding the version, then a
`Source` section holding the one key the page declares. `Descriptors` still reports one, so
the writer's guard on undeclared keys is unaffected.

### An app's own editor

**`SettingsEditorRow` is for a value no descriptor can describe**, which today means an
array of tables. The app writes an `ISettingsEditor`, the row carries it, and the window
draws it as content, so the app registers a `DataTemplate` for its own type in
`App.axaml`. `Kitbash/ViewModels/ToolRepositoriesEditor` is the one that exists, and
`Kitbash/Settings/ToolRepositoriesSettingsSchema` is the page it sits on.

**An editor owns its value and its file, and the writer never sees it.** It has no key, so
`Descriptors` reports nothing for it and the guard on undeclared keys still holds. That is
the trade for the escape hatch: rules, origin dots, layers and reset are all things a
descriptor gets and an editor writes for itself.

**It still stages.** `IsDirty` and `IsValid` join the page's, so the unsaved count, the
Save button and Discard mean what they say, and an editor counts as one change however
much is staged inside it. Save writes the page's descriptors first, then each dirty
editor, then reads the page again.

**`LoadAsync` and `SaveAsync` are awaited on the UI thread.** An editor that touches a
disk puts that part on another thread itself, since what it fills is bound to a window.
The window sets `IsPageWritable` before every load, because only the window knows that a
file is missing or will not parse.

**Reach for a descriptor first.** An editor is the answer when the shape of the value is
the problem, not when the drawing is. Anything else an app wants to draw is a reason to
change the window rather than to add a row kind.

### The schemas that exist

Five in Core, four of them in the `Application` home, plus the launcher's own three,
`LauncherCloseSettingsSchema`, `ToolRepositoriesSettingsSchema` and the State page in
`Kitbash/Settings/LauncherSettingsSchema`, which is what puts them all together into the
window's tree. That home is per user per machine and
has no layer, which is the point: **a path is right for one machine and wrong for every
other**, so none of those can be shared through a workspace's team config by accident.
`godot.engine` is the one that is genuinely a team fact, so it is the one that layers.

| Schema | Keys |
|---|---|
| `WindowSettingsSchema` | `window.nativeChrome` |
| `ExternalToolsSettingsSchema` | `tools.git.path`, `tools.dotnet.path` |
| `GodotSettingsSchema` | `godot.engines.directory`, `godot.engines.default`, `godot.build` |
| `WorkspacesSettingsSchema` | `workspaces.directory` |
| `WorkspaceGodotSettingsSchema` | `godot.engine`, and the only one in the `Workspace` home |
| `UpdateSettingsSchema` | `updates.feed`, the launcher's, and **on no page** |
| `LauncherCloseSettingsSchema` | `launcher.close.projectManager`, `launcher.close.editor`, `launcher.close.play`, the launcher's |
| `ToolRepositoriesSettingsSchema` | `tools.repositories`, the launcher's, and **through an editor rather than a descriptor** |

**A `launcher.` key is the launcher's own behaviour and Core never declares one.** Closing
after a project opens is something only the launcher can do, so the keys, the page and the
typed reader all live in `Kitbash/Settings` rather than beside the Godot keys in Core.
Read them through `ILauncherCloseSettings`, which reads at every launch, so a change
applies without a restart. The `kitbash-godot` skill has what each one follows.

**A descriptor does not have to be on a page.** `updates.feed` is the first that is not:
where releases come from is the app's answer rather than a person's, so its default is a
constant compiled into the schema and the window draws only a readout of the running
version. It is an ordinary setting otherwise, read from its descriptor out of the
application settings file, so a copy can be pointed somewhere by hand.

Two things follow. `ISettingsWriter` **refuses** a key no page declares, so nothing can
change it but an editor. And nothing announces it: the file that lists every setting from
its descriptor is the workspace scaffold, and an application setting has no such file, so
an off page key has to be written down somewhere a person will find it.

`ExternalToolsSettingsSchema` is registered by `AddKitbashApplicationStorage` alongside
the others, so a settings window can draw the page without the lookup behind it, and by
`AddKitbashExternalTools` where `IExternalTools` is what needs it.

**`workspaces.directory` defaults to blank and blank is the right default.** It is where
the clone dialog offers to put a new workspace, and there is no folder this app can guess
that would be better than asking. Reading it goes through `IWorkspacesSettings`, which
also answers blank for a relative path a hand edited file might hold, since a relative
path is not a place. `workspaces.known` and `workspaces.current` look like neighbours and
are not: those two are application state, in a different file, written by the app.

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
ends in `State`, so engines land in `%LOCALAPPDATA%\KitbashData\State\engines`. Moving it
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

What Kitbash keeps for one person on one machine, outside any workspace, so it can
be read before a workspace is known. Four places, because they are backed up, roamed
and cleared differently. `IUserDirectories` says where each one is and is the only
thing that knows the OS layout. `ApplicationPaths` names the files under them.

| | Linux | Windows |
|---|---|---|
| Configuration | `$XDG_CONFIG_HOME/kitbash` or `~/.config/kitbash` | `%APPDATA%\Kitbash`, roams |
| State | `$XDG_DATA_HOME/kitbash` or `~/.local/share/kitbash` | `%LOCALAPPDATA%\KitbashData\State` |
| Cache | `$XDG_CACHE_HOME/kitbash` or `~/.cache/kitbash` | `%LOCALAPPDATA%\KitbashData\Cache` |
| Runtime | `$XDG_RUNTIME_DIR/kitbash`, or the cache when unset | `%LOCALAPPDATA%\KitbashData\Runtime` |

An XDG variable holding a relative path is ignored, which the spec requires.

**Runtime is for things that mean nothing once this login ends**, and the single launcher
lock is its only user. `XDG_RUNTIME_DIR` has no defined fallback and the spec says so, since
a login that does not go through a session manager leaves it unset, so the cache directory
stands in. The `kitbash-updates` skill has the lock itself.

**The Windows local folder is the application name plus `Data`, and that is Velopack's
doing.** It installs the app to `%LOCALAPPDATA%\Kitbash` and its uninstaller deletes that
whole folder, so state and cache sit in a sibling rather than inside it. Nothing a person
owns may go under the install root.

**A tool's state file sits in the tool's own folder**, at `<state>/tools/<id>/state.toml`,
beside the version folders installed for it, so everything about one tool is in one place.
`ApplicationPaths.StateFileFor` is what says so and `Tools` is the directory.

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
first, and it carries `window.nativeChrome`, covered by the `kitbash-windows` skill.

**`IApplicationState`** is what the app remembers for itself. The list of workspaces,
which one is open, and window geometry when that arrives. The app writes it, a person
does not. Deleting the state directory resets Kitbash without touching anything
anyone chose, which is the whole reason it is not in the config file.

Both take the same shape, scopes and all, and both are TOML through the same
`ISettingsDocumentStore`. They share `ScopedDocuments` and differ only in which
directory their files land in. `ISettings` is the read side of both, so read its name
as a typed read over a document rather than as a claim about settings.

The cache has a directory and `ApplicationPaths.CacheFileFor`, and no API beyond that.
Nothing caches anything yet, so the first thing that does picks its own shape.


## The settings window

`Kitbash.Ui/Controls/SettingsWindow` draws whatever `SettingsSchema` it is handed, so
the launcher and every tool share one window and differ only in the pages they give it.
Its view models are `Kitbash.Ui/Settings`. The design is `Kitbash Settings v3` in the
Claude Design project.

**Every app owns its own window.** A tool's settings are not reachable from the launcher,
so no schema ever crosses a process boundary. `ISettingsWindows.Open(owner)` is how an
app opens its own, and opening it again brings the one already open to the front rather
than making a second. Register it with `AddKitbashSettingsWindow`, plus a
`SettingsSchema` of the app's own.

```csharp
services.AddKitbashSettingsSchema()
        .AddKitbashOpenWorkspaceSettings()
        .AddKitbashSettingsWindow()
        .AddSingleton(provider => provider.GetRequiredService<MyToolSettingsSchema>().Schema);
```

**The roots of the tree are the stores, not the app.** Application, Workspace and State,
because that is the question that decides which file a change lands in. A store with no
page is left out, which is also what a search that matches nothing under it leaves, and so
is one with nowhere to keep anything. Clicking a store opens nothing and puts the mark back
on the page that is open.

**A store whose places are named holds a level of them.** The launcher's Workspace store
lists every workspace by name with the same pages under each, so a person changes any
workspace's settings from one window rather than switching workspace first. A store with a
single unnamed place draws no level, which is what a tool with one workspace gets. The page
heading names the place it is writing to, since two pages under two workspaces are
otherwise the same page twice.

**The tree is built again when the window comes to the front**, since nothing watches the
workspace list any more than it watches the files. That is separate from the page reload
and synchronous, so the selection can be put back before anything reacts, and a node is
found again by its place and page rather than by reference.

**Unsaved changes are per window and Save writes only this app's files.** The footer
counts every staged change across every page that has been opened, and Save groups them
by page and layer and writes each file once. A page keeps its own changes, so switching
pages and coming back keeps them.

**There are two save buttons and they write the same way.** Save stays, and Save and close
goes when the write landed. Both run `WriteAsync`, which reports whether everything staged
was written, and only Save and close raises `Saved` on a true answer. The window closes on
that event. A page a disk or an unreadable file refused leaves its changes staged and
`Problem` set, so a false answer keeps the window up with its message whichever button was
pressed. Restart writes through `WriteAsync` too and never closes, since a restart that
could not start a copy has something to say.

**A value that a rule refuses is kept and blocks the save.** Taking it away as a person
types would be worse than refusing to write it. The row shows the reason under the
control, the origin dot turns red, and Save is disabled until every staged change is
good. Reset stages a removal rather than writing the default.

**Changing the layer is refused for a file that will not parse**, since writing it would
replace content this app could not read. See the decision recorded in
`.claude/plans/settings-schema.md`.

**A change to a setting nothing rereads offers a restart.** The row says so wherever it
stands and turns warn once there is a change waiting on it, and the footer grows a button
between Discard and Save changes. It reads Save and restart while anything is staged and
Restart now once the change is written, which is what stops a plain Save from leaving a
setting quietly not applying. A save that did not land leaves the changes alone and
nothing restarts over the top of them.

`IApplicationRestart` is what does it: a fresh copy started detached through
`IPlatformServices.StartDetached`, then `Shutdown` on Avalonia's own lifetime. It reaches
for `Application.Current`, which is the one piece of ambient state here and the only way a
library can end the app it is running in. An app launched as `dotnet app.dll` reports
dotnet as its path, so the assembly goes back on the front of the arguments. False comes
back when the copy could not be started, and then nothing has changed.

**Nothing here reads a disk on the UI thread.** A page load and a save both go through
`Task.Run`. Nothing watches the filesystem either, so the window reads every open page
again when it comes to the front, the way `IGitStatusMonitor` is driven.

**The gutter is the icon button's height whether a button is there or not.** A row that
grew when Discard or Reset appeared moved the editor beside it down four pixels, which was
visible on a toggle every time a change was staged. Measured before and after: 645,134 then
645,138 with a button, and 645,138 in every state after.

Measured on this machine, Linux, through a headless harness driving the real window: each
of the six pages drawn, a toggle and a dropdown edited and saved with the value landing
inside its own table and every comment kept, a reset removing the key and pruning the
table it emptied, a relative path refused with its reason and Save disabled, a personal
value masking a team one, a write to the team layer, a broken team file leaving personal
readable and writable while its end of the picker greys out, both files broken leaving the
page read only, no workspace drawing the empty state, a stored choice nothing offers
falling back with the reason, search filtering the tree, and the launcher rail opening the
window.

The workspace level was measured on the same harness: three workspaces listed by name, a
renamed one showing the name the resolver gives it, a page under one reading and writing
only that workspace's files, two workspaces edited at once with one Save writing each to
its own file, a workspace whose folder was deleted dropping out, one added while the window
was up appearing on the next reload with the selection still on the page it was on, no
workspaces at all leaving the store out entirely, and search keeping the level while
filtering the pages under it.

The restart was measured too: the button appearing only for a setting that carries the
flag, Save and restart writing then asking, Restart now surviving a plain Save, an invalid
change refusing both, discarding taking the button away before a save and leaving it after
one, and the failure message. The spawn itself was run for real, the harness restarting
itself both as an apphost and as `dotnet app.dll`, with the second copy reporting the path
and arguments it was given. The shutdown half was measured once under a real
`ClassicDesktopStyleApplicationLifetime`, where `Exit` fired.
