namespace Kitbash.Core.Workspaces;

/// <summary>Puts the files a workspace should always have in place.</summary>
public interface IWorkspaceScaffold
{
    /// <summary>
    /// Writes the team config file when it is not there, and does nothing when it is.
    /// Touches a disk, so keep it off the UI thread.
    /// </summary>
    void Ensure(string root);

    /// <summary>
    /// Writes the ignore rule that keeps the user layer out of git. The folder sits inside
    /// <c>.kitbash</c>, so it carries its own rule rather than relying on the repository's.
    /// </summary>
    void EnsureUserLayerIgnored(string root);
}
