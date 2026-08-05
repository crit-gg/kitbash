namespace Kitbash.Core.Git;

/// <summary>What a merge did, which git says in prose rather than in an exit code.</summary>
public enum GitMergeOutcome
{
    /// <summary>A merge commit was made and nothing is left to settle.</summary>
    Merged,

    /// <summary>The branch pointer moved forward, so there is no merge commit.</summary>
    FastForwarded,

    /// <summary>There was nothing to take.</summary>
    AlreadyLevel,

    /// <summary>It started and left conflicts in the working tree for a person to settle.</summary>
    Conflicted,

    /// <summary>
    /// A fast forward was asked for and both histories have moved, so nothing was done. A
    /// real merge is what settles it, and that is the caller's decision rather than this one's.
    /// </summary>
    NotFastForward,

    /// <summary>Uncommitted work would have been overwritten, so nothing was done.</summary>
    Blocked,

    /// <summary>Nothing here resolves to that revision.</summary>
    NoSuchRevision,

    /// <summary>The work took too long and was stopped.</summary>
    TimedOut,

    /// <summary>Git ran and failed for some other reason.</summary>
    Failed,
}

/// <summary>What a merge did, and what git said about it.</summary>
/// <param name="Message">Git's own words, trimmed. Empty when it had none.</param>
public sealed record GitMergeResult(GitMergeOutcome Outcome, string Message)
{
    /// <summary>True when the branch moved and nothing is waiting on a person.</summary>
    public bool Succeeded =>
        Outcome is GitMergeOutcome.Merged
            or GitMergeOutcome.FastForwarded
            or GitMergeOutcome.AlreadyLevel;
}

/// <summary>How a merge is run.</summary>
public sealed record GitMergeRequest
{
    public static GitMergeRequest Default { get; } = new();

    /// <summary>
    /// Refuses anything but moving the branch pointer forward, which cannot conflict and
    /// leaves no merge commit.
    /// </summary>
    public static GitMergeRequest FastForward { get; } = new() { FastForwardOnly = true };

    public bool FastForwardOnly { get; init; }

    /// <summary>Records a merge commit even where a fast forward would have done.</summary>
    public bool NoFastForward { get; init; }

    /// <summary>The message to record. Null takes git's own.</summary>
    public string? Message { get; init; }
}

/// <summary>Whether a merge would need a person, worked out without running it.</summary>
public enum GitMergeVerdict
{
    /// <summary>Git could not work it out. A revision that does not resolve reads this way.</summary>
    Unknown,

    /// <summary>It would merge with nothing left to settle.</summary>
    Clean,

    /// <summary>Some paths would need settling by hand.</summary>
    Conflicts,
}

/// <summary>
/// What merging would do, worked out against the object database alone.
/// </summary>
/// <param name="Paths">The paths that would need settling, in git's own order.</param>
public sealed record GitMergePreview(GitMergeVerdict Verdict, IReadOnlyList<string> Paths)
{
    public static GitMergePreview Unknown { get; } = new(GitMergeVerdict.Unknown, []);

    public bool IsClean => Verdict == GitMergeVerdict.Clean;
}

/// <summary>Merges one revision into another, and says beforehand what that would cost.</summary>
public interface IGitMerger
{
    /// <summary>
    /// What merging <paramref name="theirs"/> into <paramref name="ours"/> would do. It
    /// touches no working tree and moves no branch, so it is safe to run while a person reads.
    /// </summary>
    Task<GitMergePreview> PreviewAsync(
        string root,
        string theirs,
        string ours = "HEAD",
        CancellationToken cancellation = default);

    /// <summary>
    /// Merges a revision into the branch the head is on. Never rebases, since a rebase
    /// settles the same conflict once per commit.
    /// </summary>
    Task<GitMergeResult> MergeAsync(
        string root,
        string revision,
        GitMergeRequest? request = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// True when every commit in <paramref name="ancestor"/> is already in
    /// <paramref name="descendant"/>. False when either does not resolve.
    /// </summary>
    Task<bool> IsAncestorAsync(
        string root,
        string ancestor,
        string descendant,
        CancellationToken cancellation = default);
}
