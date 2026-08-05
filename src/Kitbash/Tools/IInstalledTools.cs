namespace Kitbash.Tools;

/// <summary>
/// What is installed on this machine, read from disk. A tool folder put there by hand is
/// simply a tool, since nothing but the folders themselves records what is installed.
/// </summary>
public interface IInstalledTools
{
    /// <summary>
    /// Every tool that has a usable version here, by name. Touches a disk, so call it off
    /// the UI thread. A folder it cannot make sense of is left out rather than reported.
    /// </summary>
    IReadOnlyList<InstalledTool> Read();

    /// <summary>Which version of a tool opens, which is what an install or an update sets.</summary>
    void SetActiveVersion(ToolId id, ToolVersion version);
}
