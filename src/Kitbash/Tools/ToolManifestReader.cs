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

    public int NewestFormat => 1;

    public ToolManifest Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Document? document;

        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Format);
        }
        catch (JsonException exception)
        {
            throw new ToolManifestException("The manifest is not valid JSON.", exception);
        }

        if (document is null)
        {
            throw new ToolManifestException("The manifest is empty.");
        }

        return Check(document);
    }

    public ToolManifest ReadFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var file = Path.Combine(directory, FileName);

        if (!_files.FileExists(file))
        {
            throw new ToolManifestException($"'{directory}' holds no {FileName}.");
        }

        try
        {
            return Read(_files.ReadAllText(file));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ToolManifestException($"'{file}' could not be read.", exception);
        }
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

        if (!ToolId.TryParse(document.Id, out var id) || id.Source is not null)
        {
            throw new ToolManifestException(
                $"'{document.Id}' is not a tool id. Use lower case letters, digits and dashes.");
        }

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
            Blank(document.Icon) ? null : document.Icon!.Trim(),
            version,
            document.Required,
            payloads);
    }

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

        public IReadOnlyList<PayloadDocument>? Payloads { get; init; }
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
