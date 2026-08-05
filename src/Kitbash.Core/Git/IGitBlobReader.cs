namespace Kitbash.Core.Git;

/// <summary>Reads one file as a revision holds it, rather than as a difference.</summary>
public interface IGitBlobReader
{
    /// <summary>
    /// One path as text, read as UTF 8. Null when that revision does not hold the path, which
    /// a file added or deleted on one side answers rather than failing.
    /// </summary>
    /// <param name="revision">Anything git resolves. Empty is the index.</param>
    /// <param name="path">Relative to the repository root, which is what git reports.</param>
    Task<string?> ReadTextAsync(
        string root, string revision, string path, CancellationToken cancellation = default);
}
