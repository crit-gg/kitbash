using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitbash.Core.IO;

namespace Kitbash.Tools;

/// <summary>
/// Reads <c>kitbash-tool.json</c>. JSON rather than TOML because a manifest is written by
/// a build rather than by a person, and a tool need not be written in .NET.
/// </summary>
public sealed class ToolManifestReader : IToolManifestReader
{
    /// <summary>The name a manifest always has, so nothing has to guess at it.</summary>
    public const string FileName = "kitbash-tool.json";

    /// <summary>
    /// The loose form, for a tool being worked on in a folder on this machine. It is read
    /// only for a folder somebody pointed at, never for anything downloaded.
    /// </summary>
    public const string DevelopmentFileName = "kitbash-tool.dev.json";

    /// <summary>What a folder runs as when it names no version of its own.</summary>
    public static readonly ToolVersion DevelopmentVersion = new(0, 0, 0, "dev");

    /// <summary>The format a manifest declares before it may carry a kind or any inputs.</summary>
    private const int ScriptFormat = 2;

    /// <summary>What a manifest writes for each kind.</summary>
    private const string AppWord = "app";
    private const string ScriptWord = "script";

    /// <summary>What an icon may be, since the launcher has to decode it to draw a card.</summary>
    private static readonly string[] IconKinds = [".png", ".jpg", ".jpeg", ".webp"];

    private static readonly JsonSerializerOptions Format = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _files;

    public ToolManifestReader(IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    /// <summary>Format 2 is what added the script kind and the inputs a form asks for.</summary>
    public int NewestFormat => ScriptFormat;

    public ToolManifest Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Check(Parse(json));
    }

    public ToolManifest ReadFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return Read(Text(directory, FileName));
    }

    public ToolManifest ReadDevelopment(string json, string folderName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        // The loose form carries no format number, so it is read as the newest one. It
        // never leaves this machine, so there is no older launcher to keep out of it.
        return Development(Parse(json) with { Manifest = NewestFormat }, folderName);
    }

    public ToolManifest ReadDevelopmentFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return ReadDevelopment(
            Text(directory, DevelopmentFileName),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)));
    }

    private string Text(string directory, string name)
    {
        var file = Path.Combine(directory, name);

        if (!_files.FileExists(file))
        {
            throw new ToolManifestException($"'{directory}' holds no {name}.");
        }

        try
        {
            return _files.ReadAllText(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ToolManifestException($"'{file}' could not be read.", exception);
        }
    }

    private static Document Parse(string json)
    {
        Document? document;

        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Format);
        }
        catch (JsonException exception)
        {
            throw new ToolManifestException("The manifest is not valid JSON.", exception);
        }

        return document ?? throw new ToolManifestException("The manifest is empty.");
    }

    private ToolManifest Check(Document document)
    {
        if (document.Manifest < 1)
        {
            throw new ToolManifestException("The manifest does not say which format it is.");
        }

        if (document.Manifest > NewestFormat)
        {
            throw new ToolManifestException(
                $"The manifest is format {document.Manifest} and this version of Kitbash "
                + $"reads up to {NewestFormat}. Update Kitbash to install this tool.");
        }

        if (document.Command is { Count: > 0 })
        {
            throw new ToolManifestException(
                $"A command is named in {DevelopmentFileName} alone, since a published tool "
                + "runs the payload it ships.");
        }

        var id = Identifier(document.Id);

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            throw new ToolManifestException("The manifest gives the tool no name.");
        }

        if (!ToolVersion.TryParse(document.Version, out var version))
        {
            throw new ToolManifestException($"'{document.Version}' is not a version.");
        }

        List<ToolPayload> payloads = [.. (document.Payloads ?? []).Select(Payload)];

        if (payloads.Count == 0)
        {
            throw new ToolManifestException("The manifest offers no payload for any platform.");
        }

        return new ToolManifest(
            document.Manifest,
            id.Name,
            document.Name!.Trim(),
            document.Summary?.Trim() ?? string.Empty,
            document.Category?.Trim() ?? string.Empty,
            Icon(document.Icon),
            version,
            document.Required,
            payloads,
            Kind(document))
        {
            Inputs = Inputs(document),
            TakesWorkspace = document.Workspace ?? true,
        };
    }

    /// <summary>
    /// The loose form. Everything a published manifest has to say is worked out from the
    /// folder instead, so a tool being built here declares only what it runs.
    /// </summary>
    private ToolManifest Development(Document document, string folderName)
    {
        if (Blank(document.Id) && Slug(folderName).Length == 0)
        {
            throw new ToolManifestException(
                $"'{folderName}' cannot be a tool id, so {DevelopmentFileName} has to give one.");
        }

        var id = Blank(document.Id) ? Identifier(Slug(folderName)) : Identifier(document.Id);
        var name = Blank(document.Name) ? folderName.Trim() : document.Name!.Trim();

        var version = DevelopmentVersion;

        if (!Blank(document.Version) && !ToolVersion.TryParse(document.Version, out version))
        {
            throw new ToolManifestException($"'{document.Version}' is not a version.");
        }

        List<ToolPayload> payloads = [.. (document.Payloads ?? []).Select(Payload)];
        var command = Command(document);

        if (command.Count == 0 && payloads.Count == 0)
        {
            throw new ToolManifestException(
                $"{DevelopmentFileName} names neither a command to run nor a payload.");
        }

        return new ToolManifest(
            NewestFormat,
            id.Name,
            name,
            document.Summary?.Trim() ?? string.Empty,
            document.Category?.Trim() ?? string.Empty,
            Icon(document.Icon),
            version,
            document.Required,
            payloads,
            Kind(document))
        {
            Inputs = Inputs(document),
            Command = command,
            IsDevelopment = true,
            TakesWorkspace = document.Workspace ?? true,
        };
    }

    private static ToolId Identifier(string? value)
    {
        if (!ToolId.TryParse(value, out var id) || id.Source is not null)
        {
            throw new ToolManifestException(
                $"'{value}' is not a tool id. Use lower case letters, digits and dashes.");
        }

        return id;
    }

    /// <summary>
    /// A folder or project name as a tool id. Anything an id cannot hold becomes a dash,
    /// so Sprite Import and Sprite.Import are both sprite-import.
    /// </summary>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var slug = new StringBuilder(name.Length);

        foreach (var letter in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterLower(letter) || char.IsAsciiDigit(letter))
            {
                slug.Append(letter);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }

    /// <summary>
    /// The words of a command line, the program first. A blank word is refused rather than
    /// dropped, since the program is handed a list and a blank one would reach it as an
    /// argument nobody wrote.
    /// </summary>
    private static IReadOnlyList<string> Command(Document document)
    {
        if (document.Command is not { Count: > 0 } words)
        {
            return [];
        }

        List<string> command = [];

        foreach (var word in words)
        {
            if (Blank(word))
            {
                throw new ToolManifestException("A word of the command is empty.");
            }

            command.Add(word.Trim());
        }

        return command;
    }

    /// <summary>
    /// The icon names an asset sitting beside the manifest, so anything carrying a path or
    /// an extension nothing can draw is dropped and the card keeps its letter. Both
    /// separators are refused whichever platform reads this, since only one of them is a
    /// separator here and a manifest is written somewhere else.
    /// </summary>
    private static string? Icon(string? name)
    {
        if (Blank(name))
        {
            return null;
        }

        var icon = name!.Trim();

        if (icon.AsSpan().IndexOfAny('/', '\\') >= 0
            || icon.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return IconKinds.Contains(Path.GetExtension(icon), StringComparer.OrdinalIgnoreCase) ? icon : null;
    }

    /// <summary>
    /// A manifest below the format that added the kind may not name one, so an older
    /// launcher can never read a script as though it were an app.
    /// </summary>
    private static ToolKind Kind(Document document)
    {
        if (Blank(document.Kind))
        {
            return ToolKind.App;
        }

        var kind = document.Kind!.Trim().ToLowerInvariant();

        if (kind is not (AppWord or ScriptWord))
        {
            throw new ToolManifestException(
                $"'{document.Kind}' is not a kind of tool. Use {AppWord} or {ScriptWord}.");
        }

        if (document.Manifest < ScriptFormat)
        {
            throw new ToolManifestException(
                $"A manifest naming a kind is format {ScriptFormat} or above.");
        }

        return kind == ScriptWord ? ToolKind.Script : ToolKind.App;
    }

    private static IReadOnlyList<ToolInput> Inputs(Document document)
    {
        if (document.Inputs is not { Count: > 0 } inputs)
        {
            return [];
        }

        if (document.Manifest < ScriptFormat)
        {
            throw new ToolManifestException(
                $"A manifest carrying inputs is format {ScriptFormat} or above.");
        }

        List<ToolInput> read = [];
        HashSet<string> keys = new(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            var one = Input(input);

            if (!keys.Add(one.Key))
            {
                throw new ToolManifestException($"Two inputs are both called '{one.Key}'.");
            }

            read.Add(one);
        }

        return read;
    }

    private static ToolInput Input(InputDocument input)
    {
        if (Blank(input.Key) || !IsKey(input.Key!.Trim()))
        {
            throw new ToolManifestException(
                $"'{input.Key}' is not an input key. Use letters, digits, dashes and underscores.");
        }

        var key = input.Key!.Trim();
        var kind = KindOf(key, input.Type);
        var argument = Blank(input.Argument) ? null : input.Argument!.Trim();

        // Whitespace in a flag would reach the program as two arguments, which is never
        // what the manifest meant.
        if (argument is not null && argument.Any(char.IsWhiteSpace))
        {
            throw new ToolManifestException($"The argument for '{key}' holds a space.");
        }

        if (input.Minimum is { } least && input.Maximum is { } most && most < least)
        {
            throw new ToolManifestException($"The range for '{key}' ends below where it starts.");
        }

        var one = new ToolInput(
            key,
            Blank(input.Name) ? key : input.Name!.Trim(),
            input.Description?.Trim() ?? string.Empty,
            kind,
            argument,
            input.Required,
            input.Remember,
            Default(input, kind),
            Choices(key, kind, input.Choices),
            input.Minimum,
            input.Maximum);

        // The workspace is not known here, so a default naming it is taken as given.
        if (!one.Default.Contains(ToolInput.WorkspaceToken, StringComparison.Ordinal)
            && one.Check(one.Default) is { } wrong)
        {
            throw new ToolManifestException($"The default for '{key}' is not usable. {wrong}");
        }

        return one;
    }

    private static ToolInputKind KindOf(string key, string? type)
    {
        if (Blank(type))
        {
            return ToolInputKind.Text;
        }

        if (!Enum.TryParse<ToolInputKind>(type!.Trim(), ignoreCase: true, out var kind))
        {
            throw new ToolManifestException(
                $"'{type}' is not a kind of input. '{key}' can be one of "
                + $"{string.Join(", ", Enum.GetNames<ToolInputKind>().Select(name => name.ToLowerInvariant()))}.");
        }

        return kind;
    }

    private static IReadOnlyList<ToolInputChoice> Choices(
        string key, ToolInputKind kind, IReadOnlyList<ChoiceDocument>? choices)
    {
        if (kind != ToolInputKind.Choice)
        {
            return [];
        }

        List<ToolInputChoice> read = [];

        foreach (var choice in choices ?? [])
        {
            if (Blank(choice.Value))
            {
                throw new ToolManifestException($"An option of '{key}' has no value.");
            }

            var value = choice.Value!.Trim();

            read.Add(new ToolInputChoice(value, Blank(choice.Label) ? value : choice.Label!.Trim()));
        }

        return read.Count > 0
            ? read
            : throw new ToolManifestException($"'{key}' is a choice and offers no options.");
    }

    /// <summary>
    /// Held as text whatever the kind is. A boolean writes the word rather than the JSON
    /// literal, since that is what reaches the command line.
    /// </summary>
    private static string Default(InputDocument input, ToolInputKind kind)
    {
        if (input.Default is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return kind == ToolInputKind.Boolean ? "false" : string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim(),
            JsonValueKind.True => ToolInput.True,
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new ToolManifestException("A default is text, a number or true or false."),
        };
    }

    // A key names a value in the form and in what is remembered, which is written to a
    // TOML file, so it stays inside what a bare key there allows.
    private static bool IsKey(string key) =>
        key.All(letter => char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_');

    private static ToolPayload Payload(PayloadDocument payload)
    {
        if (Blank(payload.Runtime))
        {
            throw new ToolManifestException("A payload does not say which runtime it is for.");
        }

        if (Blank(payload.Executable))
        {
            throw new ToolManifestException(
                $"The {payload.Runtime} payload does not say what to run.");
        }

        var executable = payload.Executable!.Trim();

        if (!IsInsidePayload(executable))
        {
            throw new ToolManifestException(
                $"'{executable}' is not a path inside the payload.");
        }

        return new ToolPayload(
            payload.Runtime!.Trim(),
            Blank(payload.Asset) ? null : payload.Asset!.Trim(),
            Math.Max(0, payload.Size),
            Blank(payload.Sha256) ? null : payload.Sha256!.Trim(),
            executable);
    }

    // A manifest comes off the internet and names a program the launcher then runs, so a
    // path that leaves the folder it was extracted into is refused rather than resolved.
    private static bool IsInsidePayload(string executable)
    {
        if (Path.IsPathRooted(executable) || executable.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = executable.Split('/');

        return !Array.Exists(
            segments,
            segment => segment.Length == 0 || segment is "." or ".." || IsBadName(segment));
    }

    // Windows refuses these and Linux allows most of them. Both sets are applied on both
    // platforms, so a payload that could only ever extract on one is caught on either.
    private static bool IsBadName(string segment) =>
        segment.AsSpan().IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0
        || segment.Any(char.IsControl);

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    // The shape on disk, which is every field optional so a bad one is reported in
    // English rather than as a deserialization failure.
    private sealed record Document
    {
        [JsonPropertyName("manifest")]
        public int Manifest { get; init; }

        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? Summary { get; init; }

        public string? Category { get; init; }

        public string? Icon { get; init; }

        public string? Version { get; init; }

        public bool Required { get; init; }

        /// <summary>Absent is true, so a manifest written before this reads as it did.</summary>
        public bool? Workspace { get; init; }

        public string? Kind { get; init; }

        public IReadOnlyList<PayloadDocument>? Payloads { get; init; }

        public IReadOnlyList<InputDocument>? Inputs { get; init; }

        public IReadOnlyList<string>? Command { get; init; }
    }

    private sealed record InputDocument
    {
        public string? Key { get; init; }

        public string? Name { get; init; }

        public string? Description { get; init; }

        public string? Type { get; init; }

        public string? Argument { get; init; }

        public bool Required { get; init; }

        public bool Remember { get; init; }

        /// <summary>Kept as written, since a default may be text, a number or a flag.</summary>
        public JsonElement? Default { get; init; }

        public IReadOnlyList<ChoiceDocument>? Choices { get; init; }

        public double? Minimum { get; init; }

        public double? Maximum { get; init; }
    }

    private sealed record ChoiceDocument
    {
        public string? Value { get; init; }

        public string? Label { get; init; }
    }

    private sealed record PayloadDocument
    {
        public string? Runtime { get; init; }

        public string? Asset { get; init; }

        public long Size { get; init; }

        public string? Sha256 { get; init; }

        public string? Executable { get; init; }
    }
}
