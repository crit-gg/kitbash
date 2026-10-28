namespace Kitbash.Core.Git;

/// <summary>How applying a patch ended. Everything but Applied and Stranded changed nothing.</summary>
public enum GitPatchOutcome
{
    Applied,

    /// <summary>Every change the patch makes is already here.</summary>
    AlreadyApplied,

    /// <summary>Not a file <c>git format-patch</c> wrote, so <c>git am</c> cannot read it.</summary>
    NotAPatch,

    /// <summary>A binary file is in the patch without its contents.</summary>
    MissingBinary,

    /// <summary>The patch was made against a different version of these files.</summary>
    DoesNotFit,

    /// <summary>The person's own uncommitted changes overlap what the patch changes.</summary>
    InTheWay,

    /// <summary>Git refused for another reason, such as a hook.</summary>
    Refused,

    GitMissing,
    NotARepository,

    /// <summary>The head is detached, so a commit would belong to no branch.</summary>
    NotOnBranch,

    NoCommits,

    /// <summary>A merge, rebase, cherry pick, revert, patch or conflict is under way.</summary>
    Busy,

    /// <summary>Git has no name or address to record as the committer.</summary>
    NoIdentity,

    /// <summary>Changes an earlier run set aside are still waiting to be put back.</summary>
    SetAsideWaiting,

    /// <summary>Putting things back failed. The person's changes are kept in a stash.</summary>
    Stranded,
}

/// <param name="Message">Git's own words, for a developer. Empty when git said nothing.</param>
/// <param name="Files">The paths the patch changes, or the ones in the way for InTheWay.</param>
/// <param name="Branch">The branch the commit landed on. Empty unless it was applied.</param>
/// <param name="SetAside">The stash holding the person's changes. Empty unless Stranded.</param>
public sealed record GitPatchResult(
    GitPatchOutcome Outcome,
    string Message = "",
    IReadOnlyList<string>? Files = null,
    string Branch = "",
    string SetAside = "")
{
    public IReadOnlyList<string> Paths => Files ?? [];
}

/// <param name="Subject">The commit message's first line, as <c>git am</c> will write it.</param>
/// <param name="IsMail">True when the file is the mail form <c>git format-patch</c> writes.</param>
/// <param name="MissingBinary">Binary files the patch names without carrying their contents.</param>
public sealed record GitPatchPreview(
    string Subject,
    bool IsMail,
    IReadOnlyList<GitPatch> Files,
    IReadOnlyList<string> MissingBinary)
{
    public bool CanApply => IsMail && Files.Count > 0 && MissingBinary.Count == 0;
}

/// <summary>How putting back changes an earlier run set aside ended.</summary>
public enum GitRestoreOutcome
{
    /// <summary>Nothing was set aside.</summary>
    Nothing,

    Restored,

    /// <summary>The working tree has changes of its own, so nothing was tried.</summary>
    NotClean,

    /// <summary>They would not go back cleanly. The stash is kept and nothing else moved.</summary>
    Kept,
}

/// <summary>
/// Applies a patch from <c>git format-patch</c> as a commit, around the person's own
/// uncommitted changes, with ordinary git commands. It either lands whole or changes nothing.
/// </summary>
public interface IGitPatchApplier
{
    /// <summary>What a patch file holds, read without running git.</summary>
    GitPatchPreview Read(string text);

    /// <summary>
    /// Sets aside the person's changes, runs <c>git am</c>, and puts the changes back. Any
    /// failure puts everything back as it was. Not cancellable, since stopping part way is
    /// the one thing that could leave work set aside.
    /// </summary>
    Task<GitPatchResult> ApplyAsync(string root, string patchFile);

    /// <summary>The stash an interrupted run left behind, or null when there is none.</summary>
    Task<string?> ReadSetAsideAsync(string root, CancellationToken cancellation = default);

    /// <summary>Puts back what an interrupted run set aside, only when that is safe.</summary>
    Task<GitRestoreOutcome> RestoreAsync(string root);
}
