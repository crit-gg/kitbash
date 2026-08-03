namespace Workbench.Core.Workspaces;

/// <summary>
/// The workspaces this person has added, and which one is open. The list is kept in
/// application settings, so it follows the user rather than any workspace.
/// </summary>
public interface IWorkspaceRegistry
{
    IReadOnlyList<Workspace> All { get; }

    Workspace? Current { get; }

    /// <summary>
    /// Registers a folder, creating its <c>.workbench</c> directory if it has none.
    /// Any folder can become a workspace. Adding one that is already registered just
    /// returns it.
    /// </summary>
    Workspace Add(string folder);

    /// <summary>Forgets a workspace. The folder on disk is left alone.</summary>
    void Remove(string root);

    /// <summary>
    /// Names a workspace for this person. Blank goes back to the name the workspace
    /// resolves on its own. Does nothing for a folder that is not there.
    /// </summary>
    void Rename(string root, string? name);

    void SetCurrent(string root);

    /// <summary>Reads names and states from disk again.</summary>
    void Refresh();
}
