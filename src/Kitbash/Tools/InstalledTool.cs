namespace Kitbash.Tools;

/// <summary>
/// A tool that is on this machine, at the version that runs when somebody opens it.
/// </summary>
/// <param name="Directory">The version's own folder, which is where the tool runs.</param>
/// <param name="Payload">The payload matching this machine, out of the manifest's set.</param>
/// <param name="IsLinked">
/// The folder belongs to the person rather than to Kitbash, so removing the tool forgets
/// the path and deletes nothing.
/// </param>
public sealed record InstalledTool(
    ToolId Id,
    ToolVersion Version,
    string Directory,
    ToolManifest Manifest,
    ToolPayload Payload,
    bool IsLinked = false)
{
    public string Name => Manifest.Name;

    public string Summary => Manifest.Summary;

    /// <summary>The program that gets run, wherever the payload put it.</summary>
    public string Executable => Payload.ExecutableIn(Directory);
}
