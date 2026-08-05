namespace Kitbash.Core.Git;

/// <summary>What happened to one path between two things being compared.</summary>
public enum GitChangeKind
{
    /// <summary>Nothing happened to it.</summary>
    None,

    Added,
    Modified,
    Deleted,

    /// <summary>Moved, and enough of it survived that git paired the two paths.</summary>
    Renamed,

    /// <summary>Made from another file that is still there.</summary>
    Copied,

    /// <summary>A file became a symlink or a submodule, or the other way about.</summary>
    TypeChanged,

    /// <summary>A merge left this path with more than one version and no answer.</summary>
    Unmerged,

    /// <summary>Not tracked by git at all.</summary>
    Untracked,

    /// <summary>Ignored by a rule, so git will not offer it.</summary>
    Ignored,
}

/// <summary>
/// One path that differs between two things being compared.
/// </summary>
/// <param name="Path">Relative to the repository root, with forward slashes whatever the OS.</param>
/// <param name="OldPath">Where it came from, for a rename or a copy, and null otherwise.</param>
/// <param name="Similarity">
/// How much of the old file survived a rename or a copy, as a percentage. Zero for
/// everything else.
/// </param>
public sealed record GitChange(
    GitChangeKind Kind,
    string Path,
    string? OldPath = null,
    int Similarity = 0);
