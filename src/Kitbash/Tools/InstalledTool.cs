namespace Kitbash.Tools;

/// <summary>
/// A tool that is on this machine, at the version that runs when somebody opens it.
/// </summary>
/// <param name="Directory">The version's own folder, which is where the tool runs.</param>
/// <param name="Command">The program to run and what goes in front of the arguments.</param>
/// <param name="IsLinked">
/// The folder belongs to the person rather than to Kitbash, so removing the tool forgets
/// the path and deletes nothing.
/// </param>
public sealed record InstalledTool(
    ToolId Id,
    ToolVersion Version,
    string Directory,
    ToolManifest Manifest,
    ToolCommand Command,
    bool IsLinked = false)
{
    public string Name => Manifest.Name;

    public string Summary => Manifest.Summary;

    /// <summary>The program that gets run, wherever the payload put it.</summary>
    public string Executable => Command.Program;

    /// <summary>What the program is handed before the workspace and anything a form asked for.</summary>
    public IReadOnlyList<string> Arguments => Command.Arguments;
}
