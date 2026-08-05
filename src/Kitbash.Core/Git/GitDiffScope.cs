namespace Kitbash.Core.Git;

/// <summary>Which two things a diff compares.</summary>
public enum GitDiffSide
{
    /// <summary>The index against the working tree, which is what is not staged yet.</summary>
    Unstaged,

    /// <summary>The head against the index, which is what a commit would take.</summary>
    Staged,

    /// <summary>The head against the working tree, which is everything uncommitted.</summary>
    Uncommitted,

    /// <summary>One revision against another.</summary>
    Range,

    /// <summary>One commit against its first parent, or against nothing when it has none.</summary>
    Commit,
}

/// <summary>What a diff is being taken of.</summary>
public sealed record GitDiffScope(GitDiffSide Side, string? From = null, string? To = null)
{
    public static GitDiffScope Unstaged { get; } = new(GitDiffSide.Unstaged);

    public static GitDiffScope Staged { get; } = new(GitDiffSide.Staged);

    public static GitDiffScope Uncommitted { get; } = new(GitDiffSide.Uncommitted);

    public static GitDiffScope Between(string from, string to) => new(GitDiffSide.Range, from, to);

    public static GitDiffScope Commit(string revision) => new(GitDiffSide.Commit, revision);
}

/// <summary>How the diff is taken.</summary>
/// <param name="Context">Unchanged lines kept either side of a change.</param>
/// <param name="DetectRenames">Pairs a deletion with an addition when enough of it survived.</param>
public sealed record GitDiffOptions(int Context = 3, bool DetectRenames = true)
{
    public static GitDiffOptions Default { get; } = new();

    /// <summary>Treats lines differing only in spacing as unchanged.</summary>
    public bool IgnoreWhitespace { get; init; }
}
