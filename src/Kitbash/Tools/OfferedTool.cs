namespace Kitbash.Tools;

/// <summary>
/// A version of a tool this machine could install. Offering costs nothing and the install
/// button is the only way in, which is what makes a list travelling in a clone safe.
/// </summary>
/// <param name="Payload">The payload matching this machine. A version with none is never offered.</param>
/// <param name="ManifestJson">
/// The manifest as the repository published it. Written into the install unchanged, so
/// what describes an installed version is the bytes its author signed off on.
/// </param>
public sealed record OfferedTool(
    ToolId Id,
    ToolVersion Version,
    ToolManifest Manifest,
    string ManifestJson,
    ToolPayload Payload,
    ToolRelease Release,
    ToolRepositorySource Source)
{
    /// <summary>
    /// The workspaces whose own list names this repository, in the order the workspace list
    /// holds them. Empty when the global list is what offers the tool.
    /// </summary>
    public IReadOnlyList<string> Workspaces { get; init; } = [];

    public string Name => Manifest.Name;

    public string Summary => Manifest.Summary;
}
