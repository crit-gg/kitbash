using Kitbash.Core.Settings;

namespace Kitbash.Workspaces;

/// <summary>
/// The addresses a workspace points at. The two layers add up rather than one winning,
/// so a person's own links sit after the team's instead of replacing them.
/// </summary>
public interface IWorkspaceLinks
{
    /// <summary>
    /// The open workspace's links, the team file's then the person's own, each in the
    /// order its file writes them. Empty when no workspace is open, when the files name
    /// none, or when one will not parse. Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<WorkspaceLink> Read();

    /// <summary>
    /// One workspace's own rows for one layer, exactly as spelled, including any Kitbash
    /// could not use. Touches a disk.
    /// </summary>
    /// <exception cref="SettingsFileUnreadableException">The file is there and will not parse.</exception>
    IReadOnlyList<WorkspaceLinkEntry> ReadEntries(string root, SettingsLayer layer);

    /// <summary>
    /// Replaces one layer's list, in the order given. The other layer is left alone, and
    /// so is everything else in the file. Touches a disk.
    /// </summary>
    /// <exception cref="SettingsFileUnreadableException">The file is there and will not parse.</exception>
    void Write(string root, SettingsLayer layer, IReadOnlyList<WorkspaceLinkEntry> links);
}
