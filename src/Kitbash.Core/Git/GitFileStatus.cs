namespace Kitbash.Core.Git;

/// <summary>
/// One path the working tree or the index differs on. A file can be both staged and
/// modified, which is two different changes to the same path and is why there are two.
/// </summary>
/// <param name="Path">Relative to the repository root, with forward slashes whatever the OS.</param>
/// <param name="OldPath">Where it came from, for a rename or a copy, and null otherwise.</param>
/// <param name="Staged">What the index holds against the head.</param>
/// <param name="Unstaged">What the working tree holds against the index.</param>
/// <param name="Similarity">
/// How much of the old file survived a rename or a copy, as a percentage. Zero otherwise.
/// </param>
public sealed record GitFileStatus(
    string Path,
    string? OldPath,
    GitChangeKind Staged,
    GitChangeKind Unstaged,
    int Similarity = 0)
{
    public bool IsUntracked => Unstaged == GitChangeKind.Untracked;

    public bool IsIgnored => Unstaged == GitChangeKind.Ignored;

    public bool IsUnmerged => Staged == GitChangeKind.Unmerged || Unstaged == GitChangeKind.Unmerged;
}

/// <summary>
/// Names every path a repository differs on. The status reader beside this one counts them
/// for the status bar and leaves untracked files out, which a file list cannot do.
/// </summary>
public interface IGitFileStatusReader
{
    Task<IReadOnlyList<GitFileStatus>> ReadAsync(
        string root, bool includeIgnored = false, CancellationToken cancellation = default);
}
