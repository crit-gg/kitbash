namespace Kitbash.Core.Git;

/// <summary>Which of the three versions of a conflicted file is meant.</summary>
public enum GitConflictSide
{
    /// <summary>Stage one, what both sides started from. Absent when both sides added the file.</summary>
    Base = 1,

    /// <summary>Stage two, the version on the branch being merged into.</summary>
    Ours = 2,

    /// <summary>Stage three, the version being merged in.</summary>
    Theirs = 3,
}

/// <summary>
/// One version of a conflicted file as the index holds it.
/// </summary>
/// <param name="Mode">The file mode git recorded, such as <c>100644</c>.</param>
/// <param name="ObjectId">The blob, which is how the content is read back.</param>
public sealed record GitConflictVersion(GitConflictSide Side, string Mode, string ObjectId);

/// <summary>
/// One path a merge could not settle. A side is missing when that side did not have the
/// file, which is what a delete against an edit looks like.
/// </summary>
public sealed record GitConflict(
    string Path,
    GitConflictVersion? Base,
    GitConflictVersion? Ours,
    GitConflictVersion? Theirs)
{
    /// <summary>True when one side deleted the file and the other changed it.</summary>
    public bool IsDeletion => Base is not null && (Ours is null || Theirs is null);

    /// <summary>True when both sides added a file of the same name and neither started it.</summary>
    public bool IsBothAdded => Base is null && Ours is not null && Theirs is not null;
}
