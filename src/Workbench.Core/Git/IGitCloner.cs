namespace Workbench.Core.Git;

/// <summary>Copies a repository onto this machine.</summary>
public interface IGitCloner
{
    /// <summary>
    /// Clones into a new folder named after the repository, under
    /// <paramref name="parentDirectory"/>. Refuses a destination that already exists, and
    /// removes a half written one when the clone does not finish.
    /// </summary>
    /// <param name="parentDirectory">A full path. It is created when it is not there.</param>
    Task<GitCloneResult> CloneAsync(
        GitRemote remote, string parentDirectory, CancellationToken cancellation = default);
}
