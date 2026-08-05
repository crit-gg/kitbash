namespace Kitbash.Core.Git;

/// <summary>Who git would record a commit as being by.</summary>
public sealed record GitIdentity(string Name, string Email)
{
    public override string ToString() => $"{Name} <{Email}>";
}

/// <summary>How a commit is made.</summary>
public sealed record GitCommitOptions
{
    public static GitCommitOptions Default { get; } = new();

    /// <summary>Replaces the last commit rather than adding one.</summary>
    public bool Amend { get; init; }

    /// <summary>Commits even when the index holds nothing new.</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>Stages every tracked file first, which is what <c>commit -a</c> does.</summary>
    public bool StageTracked { get; init; }

    /// <summary>Adds the trailer naming who committed it.</summary>
    public bool SignOff { get; init; }

    /// <summary>Records someone else as the author, as <c>Name and address</c>.</summary>
    public string? Author { get; init; }
}

/// <summary>Makes commits, and says who one would be by.</summary>
public interface IGitCommitter
{
    /// <summary>
    /// Commits what the index holds. Hooks run, since they are the person's own and a client
    /// that skipped them would be lying about what the repository does.
    /// </summary>
    Task<GitResult> CommitAsync(
        string root,
        string message,
        GitCommitOptions? options = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// The name and address a commit would carry, or null when either is unset. A repository
    /// with neither cannot commit, so this is what a client asks before offering to.
    /// </summary>
    Task<GitIdentity?> ReadIdentityAsync(string root, CancellationToken cancellation = default);
}
