namespace Kitbash.Core.Git;

/// <summary>
/// Runs git inside one repository. Everything that reads or changes a repository goes
/// through this, so the flags every run needs are set in one place.
/// </summary>
public interface IGitRunner
{
    /// <summary>False when there is no git on this machine.</summary>
    bool IsAvailable { get; }

    Task<GitResult> RunAsync(string root, GitCommand command, CancellationToken cancellation = default);
}
