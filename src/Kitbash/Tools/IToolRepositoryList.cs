using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;

namespace Kitbash.Tools;

/// <summary>
/// The repositories offering tools here. Availability is derived from these rather than
/// stored, so dropping an entry stops a tool being offered without touching what is
/// installed.
/// </summary>
public interface IToolRepositoryList
{
    /// <summary>
    /// The global list first, then the open workspace's, each in the order its file writes
    /// them. That order is what makes a collision refuse the same one twice running.
    /// Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ToolRepositorySource> Read();

    /// <summary>
    /// The global list first, then this workspace's own, whichever workspace is open. That
    /// is what says which tools a workspace offers from a page listing every workspace.
    /// Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ToolRepositorySource> ReadFor(Workspace workspace);

    /// <summary>
    /// The global list alone, which is the half a person edits here. Touches a disk.
    /// </summary>
    IReadOnlyList<ToolRepositorySource> ReadGlobal();

    /// <summary>
    /// Every registered workspace's list, whichever one is open. Only the open workspace
    /// offers anything, so this says which workspaces a tool travels with. Touches a disk.
    /// </summary>
    IReadOnlyList<ToolRepositorySource> ReadWorkspaces();

    /// <summary>
    /// Replaces the global list, in the order given. A workspace's own list is a team
    /// file that travels in a clone, so it is never written from here.
    /// </summary>
    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and will not parse, so nothing was written.
    /// </exception>
    void WriteGlobal(IReadOnlyList<ToolRepositorySource> repositories);
}
