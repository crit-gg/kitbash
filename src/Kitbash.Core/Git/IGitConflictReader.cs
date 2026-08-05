namespace Kitbash.Core.Git;

/// <summary>What a repository is in the middle of, which decides how a conflict is abandoned.</summary>
public enum GitMergeState
{
    /// <summary>Nothing is under way.</summary>
    None,

    Merging,
    Rebasing,
    CherryPicking,
    Reverting,
}

/// <summary>
/// Reads the three versions a conflict leaves in the index, and settles one.
/// </summary>
public interface IGitConflictReader
{
    /// <summary>Every path the index holds more than one version of.</summary>
    Task<IReadOnlyList<GitConflict>> ReadAsync(
        string root, CancellationToken cancellation = default);

    /// <summary>What the repository is part way through, which says what can undo it.</summary>
    Task<GitMergeState> ReadStateAsync(string root, CancellationToken cancellation = default);

    /// <summary>
    /// One version of a conflicted file, as text. Null when that side has no version. Read as
    /// UTF 8, so this is for text and a binary file has to be read another way.
    /// </summary>
    Task<string?> ReadVersionAsync(
        string root,
        GitConflict conflict,
        GitConflictSide side,
        CancellationToken cancellation = default);

    /// <summary>
    /// Says a path is settled, by putting what the working tree holds into the index. The
    /// caller has to have written the settled content there first.
    /// </summary>
    Task<GitResult> ResolveAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default);

    /// <summary>
    /// Settles a path by taking one whole side of it, working tree and index both.
    /// </summary>
    Task<GitResult> TakeAsync(
        string root,
        IReadOnlyList<string> paths,
        GitConflictSide side,
        CancellationToken cancellation = default);

    /// <summary>Abandons whatever is under way and puts the working tree back.</summary>
    Task<GitResult> AbortAsync(string root, CancellationToken cancellation = default);
}
