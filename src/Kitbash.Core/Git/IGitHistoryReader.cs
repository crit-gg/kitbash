namespace Kitbash.Core.Git;

/// <summary>Which commits to walk, and how many of them.</summary>
/// <param name="Limit">How many to return. A history view asks for a screenful at a time.</param>
/// <param name="Skip">How many to pass over first, for the next page.</param>
public sealed record GitHistoryQuery(int Limit = 200, int Skip = 0)
{
    /// <summary>
    /// Where to start walking. Empty is the head. Anything git accepts as a revision goes
    /// here, including a range such as <c>origin/main..HEAD</c>.
    /// </summary>
    public IReadOnlyList<string> Revisions { get; init; } = [];

    /// <summary>Walks every branch and tag rather than the head alone.</summary>
    public bool EveryRef { get; init; }

    /// <summary>Only commits that touched this path, relative to the repository root.</summary>
    public string? Path { get; init; }

    /// <summary>Follows the first parent of a merge only, so a branch reads as one line.</summary>
    public bool FirstParentOnly { get; init; }
}

/// <summary>Reads the commits a repository holds.</summary>
public interface IGitHistoryReader
{
    /// <summary>
    /// Walks the history. Empty when git could not read it, which a repository with no
    /// commits yet also answers.
    /// </summary>
    Task<IReadOnlyList<GitCommit>> ReadAsync(
        string root, GitHistoryQuery query, CancellationToken cancellation = default);

    /// <summary>One commit by any revision git accepts, or null when there is no such thing.</summary>
    Task<GitCommit?> ReadOneAsync(
        string root, string revision, CancellationToken cancellation = default);
}
