namespace Kitbash.Core.Git;

/// <summary>Makes a folder a git repository.</summary>
public interface IGitInitializer
{
    /// <summary>
    /// Runs <c>git init</c> in a folder that is already there. False when git is missing,
    /// refused or already has a repository there, and the workspace is made either way.
    /// </summary>
    Task<bool> InitialiseAsync(string directory, CancellationToken cancellation = default);
}
