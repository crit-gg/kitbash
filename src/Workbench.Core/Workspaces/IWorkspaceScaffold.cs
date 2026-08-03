namespace Workbench.Core.Workspaces;

/// <summary>Puts the files a workspace should always have in place.</summary>
public interface IWorkspaceScaffold
{
    /// <summary>
    /// Writes the team config file when it is not there, and does nothing when it is.
    /// Touches a disk, so keep it off the UI thread.
    /// </summary>
    /// <remarks>
    /// **Never fails.** A folder that cannot be written to is somebody else's checkout or
    /// a read only mount, and a workspace works without this file, so a refusal is not
    /// worth taking the app down for.
    /// </remarks>
    void Ensure(string root);
}
