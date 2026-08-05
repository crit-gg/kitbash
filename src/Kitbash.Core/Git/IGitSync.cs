namespace Kitbash.Core.Git;

/// <summary>What came back from talking to a remote.</summary>
public enum GitSyncOutcome
{
    /// <summary>It worked and something moved.</summary>
    Done,

    /// <summary>It worked and there was nothing to do.</summary>
    AlreadyLevel,

    /// <summary>
    /// The remote refused, because this branch is behind it. Fetching and taking the new
    /// commits first is the only way through.
    /// </summary>
    Rejected,

    /// <summary>The branch tracks nothing, so there is no default place to send it.</summary>
    NoUpstream,

    /// <summary>There is no remote at all.</summary>
    NoRemote,

    /// <summary>
    /// The two histories have both moved, so a fast forward is not possible. A merge is what
    /// settles it, and that is the caller's decision rather than this one's.
    /// </summary>
    Diverged,

    /// <summary>The working tree holds changes that the incoming commits would overwrite.</summary>
    Blocked,

    /// <summary>The merge started and left conflicts to settle.</summary>
    Conflicted,

    /// <summary>Git wanted a password, a passphrase or a host key answer, and there is nowhere to ask.</summary>
    NeedsCredentials,

    /// <summary>The work took too long and was stopped.</summary>
    TimedOut,

    /// <summary>Git ran and failed. Offline, a rejected key, a missing branch, anything else.</summary>
    Failed,
}

/// <summary>What talking to a remote did, and what git said about it.</summary>
/// <param name="Message">Git's own words, trimmed. Empty when it had none.</param>
public sealed record GitSyncResult(GitSyncOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome is GitSyncOutcome.Done or GitSyncOutcome.AlreadyLevel;
}

/// <summary>How a push is sent.</summary>
public sealed record GitPushRequest
{
    public static GitPushRequest Default { get; } = new();

    /// <summary>Where to send it. Null takes the branch's own upstream, or the only remote.</summary>
    public string? Remote { get; init; }

    /// <summary>What to send. Null is the current branch.</summary>
    public string? Branch { get; init; }

    /// <summary>Records the remote branch as what this one tracks from now on.</summary>
    public bool SetUpstream { get; init; }

    /// <summary>
    /// Overwrites the remote branch, but only when it is still where this copy last saw it.
    /// Someone else's commits are lost by this, so it is never a repair to reach for first.
    /// </summary>
    public bool ForceWithLease { get; init; }

    /// <summary>Sends the tags reachable from what is being pushed.</summary>
    public bool WithTags { get; init; }
}

/// <summary>How new commits are taken.</summary>
public sealed record GitPullRequest
{
    public static GitPullRequest Default { get; } = new();

    /// <summary>Where to take them from. Null takes the branch's own upstream.</summary>
    public string? Remote { get; init; }

    /// <summary>Which branch to take. Null takes the branch's own upstream.</summary>
    public string? Branch { get; init; }

    /// <summary>
    /// True to refuse anything but moving the branch pointer forward, which cannot conflict
    /// and leaves no merge commit. False allows a real merge when the histories have parted.
    /// </summary>
    public bool FastForwardOnly { get; init; } = true;
}

/// <summary>Sends commits to a remote, takes them from one, and says which there are.</summary>
public interface IGitSync
{
    /// <summary>
    /// Every remote this repository has, by name, in git's own order. Empty is a repository
    /// with nowhere to send anything, which is a working repository rather than a broken one.
    /// </summary>
    Task<IReadOnlyList<string>> ReadRemotesAsync(
        string root, CancellationToken cancellation = default);

    /// <summary>Brings the remote tracking refs up to date without touching any branch.</summary>
    Task<GitSyncResult> FetchAsync(
        string root, string? remote = null, CancellationToken cancellation = default);

    Task<GitSyncResult> PushAsync(
        string root, GitPushRequest? request = null, CancellationToken cancellation = default);

    /// <summary>
    /// Fetches, then moves the branch onto what came down. Never rebases: a rebase settles
    /// the same conflict once per commit, which for a binary file is once too many.
    /// </summary>
    Task<GitSyncResult> PullAsync(
        string root, GitPullRequest? request = null, CancellationToken cancellation = default);
}
