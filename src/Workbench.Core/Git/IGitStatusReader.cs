namespace Workbench.Core.Git;

/// <summary>Reads the state of a repository.</summary>
public interface IGitStatusReader
{
    /// <summary>Whether git is installed and can be run at all.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Reads the repository at a folder, or null when there is not one, when git is not
    /// installed, or when git refused to answer.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception, because a folder that is not a repository is an
    /// ordinary answer and every caller of this has something else to show in that case.
    /// </remarks>
    Task<GitStatus?> ReadAsync(string root, CancellationToken cancellation = default);
}
