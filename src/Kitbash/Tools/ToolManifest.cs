namespace Kitbash.Tools;

/// <summary>
/// What a tool says about itself. One of these is published beside every release and a
/// copy of it sits in the installed version's folder, so a tool describes itself offline.
/// </summary>
/// <param name="Format">
/// The version of this file format, not of the tool. A number this launcher does not know
/// is refused rather than guessed at.
/// </param>
/// <param name="Required">
/// The tool refuses to start on any version below this one until somebody updates it.
/// </param>
/// <param name="Icon">
/// An image published beside the manifest, or null for a tool that draws a letter instead.
/// A plain file name, never a path.
/// </param>
public sealed record ToolManifest(
    int Format,
    string Id,
    string Name,
    string Summary,
    string Category,
    string? Icon,
    ToolVersion Version,
    bool Required,
    IReadOnlyList<ToolPayload> Payloads,
    ToolKind Kind = ToolKind.App)
{
    /// <summary>
    /// What the form asks for before a script runs. The order is the order it is drawn in
    /// and the order the answers reach the command line. Empty draws no form.
    /// </summary>
    public IReadOnlyList<ToolInput> Inputs { get; init; } = [];

    /// <summary>The launcher waits for it and reports on it rather than letting it go.</summary>
    public bool IsScript => Kind == ToolKind.Script;
}

/// <summary>
/// One platform's copy of a tool. Everything about downloading it is optional, since a
/// folder placed by hand has nothing to download and still has something to run.
/// </summary>
/// <param name="Runtime">
/// A .NET runtime identifier such as <c>linux-x64</c>, or <c>any</c> for a payload the
/// author says runs everywhere.
/// </param>
/// <param name="Asset">The file to fetch from the release.</param>
/// <param name="Size">The asset's length in bytes, or zero when it does not say.</param>
/// <param name="Executable">A relative path inside the payload. This is what gets run.</param>
public sealed record ToolPayload(
    string Runtime,
    string? Asset,
    long Size,
    string? Sha256,
    string Executable)
{
    /// <summary>The runtime a payload names when it claims to run on every platform.</summary>
    public const string AnyRuntime = "any";

    /// <summary>Where the executable sits under an installed version's folder.</summary>
    public string ExecutableIn(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        // A manifest writes the path with forward slashes, since that is what an archive
        // holds whichever platform packed it.
        return Path.Combine([directory, .. Executable.Split('/')]);
    }
}
