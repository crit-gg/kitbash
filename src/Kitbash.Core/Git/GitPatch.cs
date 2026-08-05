namespace Kitbash.Core.Git;

/// <summary>What one line of a hunk is.</summary>
public enum GitDiffLineKind
{
    /// <summary>Present on both sides.</summary>
    Context,

    Added,
    Removed,

    /// <summary>
    /// The <c>\ No newline at end of file</c> note. It belongs to the line above it and has
    /// to be written back out, or a patch built from these hunks adds a newline nobody asked
    /// for.
    /// </summary>
    NoNewline,
}

/// <summary>
/// One line of a hunk.
/// </summary>
/// <param name="Text">The line without its leading marker and without its newline.</param>
/// <param name="OldLine">Its number on the old side, or null when it is not on that side.</param>
/// <param name="NewLine">Its number on the new side, or null when it is not on that side.</param>
public sealed record GitDiffLine(
    GitDiffLineKind Kind,
    string Text,
    int? OldLine = null,
    int? NewLine = null);

/// <summary>
/// One line of a patch, named by the hunk it is in and its place in that hunk. What a
/// gesture picked, for staging less than a whole hunk.
/// </summary>
/// <param name="Hunk">The hunk's index in <see cref="GitPatch.Hunks"/>, counting from zero.</param>
/// <param name="Line">The line's index in <see cref="GitHunk.Lines"/>, counting from zero.</param>
public readonly record struct GitPatchLine(int Hunk, int Line);

/// <summary>
/// One run of changed lines and the context around it.
/// </summary>
/// <param name="OldStart">The first line number on the old side, counting from one.</param>
/// <param name="OldCount">How many lines of the old side this covers.</param>
/// <param name="NewStart">The first line number on the new side, counting from one.</param>
/// <param name="NewCount">How many lines of the new side this covers.</param>
/// <param name="Heading">
/// What git wrote after the second <c>@@</c>, usually the enclosing function. Never read by
/// anything, so it is carried only so a rewritten hunk still looks like git's.
/// </param>
public sealed record GitHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    string Heading,
    IReadOnlyList<GitDiffLine> Lines)
{
    public bool HasChanges => Lines.Any(l => l.Kind is GitDiffLineKind.Added or GitDiffLineKind.Removed);
}

/// <summary>
/// The difference in one file.
/// </summary>
/// <param name="Path">The path on the new side, or the old one for a deletion.</param>
/// <param name="OldPath">Where it came from, for a rename or a copy, and null otherwise.</param>
/// <param name="Header">
/// Every line from <c>diff --git</c> down to the first hunk, kept as git wrote it. A patch
/// built from a subset of the hunks reuses these unchanged, since they carry the modes and
/// the blob names git checks the patch against.
/// </param>
/// <param name="IsBinary">
/// True when git would not write lines for it. There are no hunks in that case, whatever
/// else is here.
/// </param>
public sealed record GitPatch(
    string Path,
    string? OldPath,
    GitChangeKind Kind,
    bool IsBinary,
    IReadOnlyList<string> Header,
    IReadOnlyList<GitHunk> Hunks)
{
    public int AddedLines => Hunks.Sum(h => h.Lines.Count(l => l.Kind == GitDiffLineKind.Added));

    public int RemovedLines => Hunks.Sum(h => h.Lines.Count(l => l.Kind == GitDiffLineKind.Removed));
}
