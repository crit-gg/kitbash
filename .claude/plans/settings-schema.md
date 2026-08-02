# Settings schemas and the settings window

Describe a setting once, as an object, so a settings window can be built over any app's
settings without that window knowing anything about the app.

Not a Slate stage. It sits beside the thirteen and depends on stage 8 for the input
controls and stage 9 for the tree, so it lands after those.

## Goal

Today a setting is a key literal and a default at the point of the read.
`WindowSettings` is the only typed reader and it holds `window.nativeChrome` as a private
const with `false` written at the call site. There is nowhere for a name, a description,
a bound or a choice list to live, and nothing can enumerate what exists.

After this, a descriptor is the one place a setting is defined, the reader takes its
default from it, and the window draws every app's settings from the same layout.

```
dotnet run --project src/Workbench      then open Settings
dotnet run --project src/tools/<tool>   then open Settings, same window, its own tree
```

## Source of truth

`Workbench Settings v3.dc.html` in the design project. Read it before working this,
along with `uploads/settings-system.md` in the same project, which is the written
account of the storage model the design was drawn against.

`Workbench Settings.dc.html` and its v2 are earlier drafts. v2 put every app's settings
in the launcher and v3 undoes that. Do not work from either.

The seven design notes on the v3 page are the argument for the shape below. The ones that
decide code:

1. **Every app owns its own settings window.** Foundry's settings are not reachable from
   the launcher, so no schema ever crosses a process boundary.
2. **One layout, two instances.** A third tool costs a schema and no new UI.
3. **The top of the tree is the store, not the app.** Application, Workspace, State.
4. **Each window writes only to its own files.** The launcher writes `workbench.toml`,
   a tool writes `tools/<id>.toml`. The layer picker still chooses team or personal.
5. **Unsaved changes are per window.** The footer counts this app's edits and Save writes
   only this app's files.
7. **Validation reports at two levels.** A bad value fails one row and the setting keeps
   its fallback. A file that will not parse fails the whole page and the banner names it.

## The model

Three levels, because the design has three: a tree node is a page, a page holds sections
under rule headings, and a section holds rows.

```csharp
public sealed class SettingsSchema
{
    public SettingsSchema(SettingsScope scope, IReadOnlyList<SettingsPage> pages);
    public SettingsScope Scope { get; }
    public IReadOnlyList<SettingsPage> Pages { get; }
}

public sealed class SettingsPage
{
    public required string Id { get; init; }
    public required string Title { get; init; }          // tree node and page heading
    public required SettingsHome Home { get; init; }     // Application, Workspace, State
    public required IReadOnlyList<SettingsSection> Sections { get; init; }
    public bool IsReadOnly { get; init; }
}

public sealed record SettingsSection(string Title, IReadOnlyList<ISettingsRow> Rows);

public sealed class SettingDescriptor<T> : ISettingsRow where T : notnull
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }    // drawn under the name
    public required T Default { get; init; }
    public IReadOnlyList<ISettingRule<T>> Rules { get; init; } = [];
    public ISettingChoices<T>? Choices { get; init; }
    public ISettingProbe? Probe { get; init; }
    public SettingEditor Editor { get; init; }
    public string? Unit { get; init; }
    public bool IsReadOnly { get; init; }

    public T Read(ISettings settings) => settings.Get(Key, Default);
}
```

**The scope belongs to the schema, not to a setting.** Every page in the launcher's
window writes `workbench.toml` and every page in a tool's writes that tool's file, across
both homes. Fixing it once at composition means a tool cannot declare a global key by
accident and write into the file the whole team shares.

**The home belongs to the page.** It decides whether the layer picker appears, which
files fill the backing table and what the scope line under the title says. There is no
per setting home.

**A descriptor is an instance member, reached through a constructor.** A
`public static readonly` descriptor is the obvious shortcut and it is ambient state, so
`WindowSettings` takes the schema in and reads `_schema.NativeChrome.Read(settings)`.

**`Description` is required and is drawn.** The v3 row is the name over the description
and the key is gone from the row entirely, surviving only in search. Where a setting only
takes effect later, that goes in the description as a sentence, which is what the design
does with `window.nativeChrome`. There is no structured field for it.

**Editor is derived unless set.** A bool is a toggle, a closed choice is a segment when
the options are few and short and a dropdown otherwise, a number is a number with its
unit, a string is text, an array is a list. Set it by hand only where the derivation is
wrong.

**Types are limited to what a document can hold.** `SettingsValueConverter` handles
string, bool, the integer and floating types, enums as their name, and arrays of those.
Nested tables are out.

## Rules, choices and probes are three different things

**A rule is data, never a predicate.** A `Func<T, bool>` can validate and nothing else.
It cannot bound a spinner, cannot write its own summary and cannot be reasoned about. So
the set is closed and each rule carries its check and a plain language summary. Adding a
rule kind is a deliberate change.

| Rule | Carries |
|---|---|
| `RangeRule<T>` | minimum, maximum, either optional |
| `LengthRule` | min and max characters |
| `NotBlankRule` | nothing |
| `PatternRule` | a regex and its own summary, since a regex is not a message |
| `ChoiceRule<T>` | value, label and description per option |
| `PathShapeRule` | file or directory, rooted or not |

**A choice source is not a rule.** `ui.theme` is a closed `ChoiceRule`, so `"midnight"`
is invalid and the default takes over. `engine.version` is a list of installed engines
discovered at runtime, so a pinned version that is not installed is still the right value
and must be kept. The design draws exactly this difference. A source offers options and
never invalidates.

**A probe is the environment, not the value.** Whether an engine is installed or a folder
exists cannot run inside a pure check, and the answer changes after the value is written.

```csharp
public interface ISettingProbe
{
    /// <summary>Runs off the UI thread. Null when there is nothing to say.</summary>
    Task<string?> Inspect(object? value, CancellationToken token);
}
```

The page draws its rows first and fills the message in when the probe answers.

**One message slot, two causes, and the dot says which.** A rule or conversion failure
means the value did not survive, so the origin dot turns red and the effective value falls
back. A probe message means the value survived and the world is wrong, so the dot keeps
the colour of the layer it came from. That is the whole of severity. No severity field.

## What Core owes

Four gaps, all named in `uploads/settings-system.md` as missing, all needed before a
window can be honest.

**Per layer reads.** `ISettings` merges and forgets which layer won. The origin dot, its
tooltip, whether reset is offered, and the trap where a Team shared write is masked by a
personal override all need a read that names its layer.

**A batched write, and a remove behind it.** Save writes every staged edit at once, so a
page's changes are one read and one write per file rather than one of each per key. Reset
removes the key rather than writing today's default, which would otherwise pin it forever
and stop tracking a default that later changes.

**File existence and parse state per layer.** `WorkspacePaths.FileFor` and
`ApplicationPaths.SettingsFileFor` are already public. The backing table needs ok or
missing beside each, and a parse failure has to arrive as a value rather than an
exception, since it fills the banner.

**A writer that refuses a layer it could not read.** See the hazard below.

The read side comes back as one page shaped value.

```csharp
public interface ISettingsInspector
{
    SettingsPageView Read(SettingsPage page);
}

public sealed record SettingsPageView(
    IReadOnlyList<SettingsFileView> Files,
    IReadOnlyList<SettingValueView> Values);

public sealed record SettingsFileView(
    SettingsLayer Layer,
    string Path,
    bool Exists,
    string? ParseError);

public sealed record SettingValueView(
    ISettingsRow Row,
    object? Effective,
    SettingOrigin Origin,              // Default, TeamShared, User, File, Invalid
    bool DiffersFromDefault,
    SettingProblem? Problem,
    IReadOnlyList<SettingLayerValue> Layers);

public sealed record SettingLayerValue(
    SettingsLayer Layer,
    object? Raw,
    bool IsUsable,
    SettingCheck Check);

public sealed record SettingProblem(string Message, SettingsSource? Source);
```

`Layers` closes three cases at once: which layer won, that a lower layer also holds a
value, and that a stored value could not be used. Without it the dot is decoration.

## Hazards

**A file that will not parse must never be written back.** `Set` reads the file, sets one
key and writes the whole document. Against a broken file the read throws today, which is
the correct accident. The moment the inspector catches parse errors to draw a banner, the
same catch would leave the page holding an empty document over a file full of real
content, and Save would rewrite it as almost nothing. Every hand written value would be
gone and the button that did it would have said Save changes. The writer refuses on the
parse result, not on the document being empty, because after the fact those look
identical.

**Reset removes, it does not write the default.** Writing the default pins today's value
and quietly opts that person out of every future change to it.

**Nothing watches the filesystem.** Every tool is its own process and a write in one is
invisible to another until it reloads. The settings window reloads when it comes to the
front, the way `IGitStatusMonitor` is driven from `Activated`.

**Nothing on the UI thread reads a disk.** The page load, every probe and every save go
through the `Read` then `Apply` shape from `LauncherViewModel`.

**Paths are displayed through `IPathShortener`.** The design writes
`%APPDATA%/Workbench/workbench.toml`, which is the Windows form of one of three per OS
layouts. The backing table shows whatever that OS calls the place.

**Two rows in the design are not settings.** The state page draws the known workspace list
with the open one marked, and a red Reset Workbench button. Neither has a default or a
rule, so `ISettingsRow` has a second implementation the app supplies with its own content.
One escape hatch, used twice, so the schema never grows a way to describe a button.

## Where it lives

```
src/Workbench.Core/Settings/Schema/     descriptors, pages, rules, choices, probes
src/Workbench.Core/Settings/            the inspector, the writer, the read side gaps
src/Workbench.Ui/Controls/              the window that draws any schema
src/Workbench.Gallery/                  a page hosting the window over a sample schema
```

Core gains no dependency. The window takes a `SettingsSchema` plus the inspector and the
writer, so a tool writes a schema class and one line to open it.

## Open decisions

Do not assume this. Ask before building on it.

- **Does a broken file kill the page or only its own layer?** The banner says nothing on
  the page can be read, but a broken team file leaves the personal file perfectly good.
  Disabling the tab for the broken layer and keeping Save for the other is a smaller claim
  and loses nothing. Nothing in Core rests on the answer, since the parse error is
  recorded per file and both readings are drawn from the same `SettingsPageView`.

## Decided

- **Undeclared keys are not reported.** A key in a file that no descriptor declares is
  drawn nowhere, so the window is a complete account of the settings rather than of the
  file. `SettingsDocument` therefore never grows a key listing, and the backing row's
  status cell says only whether the file is there and parses.

`errorSource` is carried in the design's data as an offending TOML line and once as a file
and a line number, and nothing draws it. Line numbers mean every leaf records where it
came from, which Tomlyn can supply and which stays format neutral as a file and a line,
but it widens the one class that keeps TOML out of everything else. Leave it until the
design draws it.

## Build

Each step leaves the app working.

1. **Schema types and rules.** Move `window.nativeChrome` onto a descriptor and have
   `WindowSettings` read its default from it. Nothing else changes.
2. **The read and write gaps.** Per layer reads, remove, batched write, file existence,
   parse state as a value, and the write refusal that goes with it.
3. **The inspector and the page view.**
4. **The window in `Workbench.Ui`**, plus the gallery page. Editors: toggle, select,
   segment, number with unit, text, read only list, and the app supplied row.
5. **The launcher's schema**, which is the first real one, and the Settings page the rail
   already draws disabled.
6. **Probes**, last. Every page is useful before any of them exist.
