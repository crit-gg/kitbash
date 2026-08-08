namespace Kitbash.Core.Git;

/// <summary>Lists branches and moves between them.</summary>
public interface IGitBranches
{
    /// <summary>
    /// Every branch, local and remote tracking, newest commit first. Empty when there is no
    /// repository or it has no commits yet.
    /// </summary>
    Task<IReadOnlyList<GitBranch>> ReadAsync(
        string root, bool includeRemote = true, CancellationToken cancellation = default);

    /// <summary>
    /// The full ref name of every branch whose commits are all in <paramref name="into"/>,
    /// which is what says a branch has nothing left on it. The named branch is one of them.
    /// </summary>
    Task<IReadOnlyList<string>> ReadMergedAsync(
        string root,
        string into,
        bool includeRemote = true,
        CancellationToken cancellation = default);

    /// <summary>
    /// Makes a branch, and moves to it unless told not to. A start point is anything git
    /// accepts as a revision, and null means the head.
    /// </summary>
    /// <param name="track">
    /// False passes <c>--no-track</c>. Git otherwise gives a branch started from a remote one
    /// that branch as its upstream, so a branch made from <c>origin/main</c> tracks main.
    /// </param>
    Task<GitResult> CreateAsync(
        string root,
        string name,
        string? startPoint = null,
        bool switchTo = true,
        bool track = true,
        CancellationToken cancellation = default);

    /// <summary>
    /// Records what a branch tracks, or unsets it when <paramref name="upstream"/> is null.
    /// The upstream is a short name such as <c>origin/main</c>.
    /// </summary>
    Task<GitResult> SetUpstreamAsync(
        string root,
        string branch,
        string? upstream,
        CancellationToken cancellation = default);

    /// <summary>
    /// Moves to a branch. Naming one that only a remote has makes the local branch that
    /// tracks it, which is what git does on its own when the name is unambiguous.
    /// </summary>
    Task<GitResult> SwitchAsync(string root, string name, CancellationToken cancellation = default);

    /// <summary>Moves to a commit, leaving the head detached.</summary>
    Task<GitResult> SwitchToCommitAsync(
        string root, string revision, CancellationToken cancellation = default);

    /// <summary>
    /// Deletes a local branch. Without <paramref name="force"/> git refuses one holding work
    /// that is nowhere else.
    /// </summary>
    Task<GitResult> DeleteAsync(
        string root, string name, bool force = false, CancellationToken cancellation = default);

    Task<GitResult> RenameAsync(
        string root, string from, string to, CancellationToken cancellation = default);
}
