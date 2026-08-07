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

    /// <summary>
    /// Records the repository a version was installed from, so a tool a workspace provides
    /// can be told from one the global list provides without asking the network.
    /// </summary>
    void SetOrigin(ToolId id, ToolRepositorySource source);

    /// <summary>
    /// Points a tool at a folder somewhere else on this machine, which is then where it
    /// runs from. Nothing is copied, so a rebuild in that folder is picked up as it is.
    /// </summary>
    void Link(ToolId id, string directory);

    /// <summary>Forgets the folder a tool was pointed at. The folder itself is untouched.</summary>
    void Unlink(ToolId id);
}
