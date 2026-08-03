namespace Kitbash.Core.Git;

/// <summary>Reads the state of a repository.</summary>
public interface IGitStatusReader
{
    /// <summary>Whether git is installed and can be run at all.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Reads the repository at a folder, or null when there is not one, when git is not
    /// installed, or when git refused to answer.
    /// </summary>
    Task<GitStatus?> ReadAsync(string root, CancellationToken cancellation = default);
}
