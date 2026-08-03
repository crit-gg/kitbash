namespace Workbench.Core.Workspaces;

/// <summary>Puts the files a workspace should always have in place.</summary>
public interface IWorkspaceScaffold
{
    /// <summary>
    /// Writes the team config file when it is not there, and does nothing when it is.
    /// Touches a disk, so keep it off the UI thread.
    /// </summary>
    void Ensure(string root);
}
