namespace Kitbash.Core.Git;

/// <summary>
/// Moves changes into and out of the index, whole files or single hunks. Every call answers
/// with what git said, since the only failures here are git refusing and saying why.
/// </summary>
public interface IGitStager
{
    /// <summary>Stages whole paths, deletions and untracked files included.</summary>
    Task<GitResult> StageAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>Stages everything the working tree holds.</summary>
    Task<GitResult> StageAllAsync(string root, CancellationToken cancellation = default);

    /// <summary>Takes whole paths back out of the index, leaving the working tree alone.</summary>
    Task<GitResult> UnstageAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>
    /// Throws away working tree changes and puts the index version back. There is no undo
    /// for this, since what it discards was never given to git.
    /// </summary>
    Task<GitResult> DiscardAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>
    /// Deletes untracked files. There is no undo for this either, and git will not do it to
    /// an ignored file unless it is named.
    /// </summary>
    Task<GitResult> DeleteUntrackedAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>
    /// Records an untracked file as one git means to track, with no content. That is what
    /// makes it show in a diff, so a hunk of a new file can be staged like any other.
    /// </summary>
    Task<GitResult> BeginTrackingAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>
    /// Stages some of a file's hunks. The patch must be the unstaged one, since that is what
    /// describes the index this is applied to.
    /// </summary>
    /// <param name="hunks">Indexes into the patch's own hunk list.</param>
    Task<GitResult> StageHunksAsync(
        string root,
        GitPatch patch,
        IReadOnlyCollection<int> hunks,
        CancellationToken cancellation = default);

    /// <summary>
    /// Takes some of a file's hunks back out of the index. The patch must be the staged one.
    /// </summary>
    /// <param name="hunks">Indexes into the patch's own hunk list.</param>
    Task<GitResult> UnstageHunksAsync(
        string root,
        GitPatch patch,
        IReadOnlyCollection<int> hunks,
        CancellationToken cancellation = default);
}
