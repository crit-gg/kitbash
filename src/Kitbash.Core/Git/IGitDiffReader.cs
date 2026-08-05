namespace Kitbash.Core.Git;

/// <summary>Reads what changed, either as a list of paths or as lines.</summary>
public interface IGitDiffReader
{
    /// <summary>
    /// Every path that differs, without reading any content. This is what a file list is
    /// drawn from, since a repository can differ in more files than anyone would open.
    /// </summary>
    Task<IReadOnlyList<GitChange>> ReadChangesAsync(
        string root, GitDiffScope scope, CancellationToken cancellation = default);

    /// <summary>
    /// The lines that differ, for the paths given, or for everything when none are.
    /// </summary>
    Task<IReadOnlyList<GitPatch>> ReadPatchesAsync(
        string root,
        GitDiffScope scope,
        IReadOnlyList<string>? paths = null,
        GitDiffOptions? options = null,
        CancellationToken cancellation = default);

    /// <summary>The lines that differ in one file, or null when it does not differ.</summary>
    Task<GitPatch?> ReadPatchAsync(
        string root,
        GitDiffScope scope,
        string path,
        GitDiffOptions? options = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// A file git does not track yet, read as though every line of it were added. Nothing is
    /// staged to do it, so looking at a new file leaves the index alone.
    /// </summary>
    Task<GitPatch?> ReadUntrackedPatchAsync(
        string root, string path, CancellationToken cancellation = default);
}
