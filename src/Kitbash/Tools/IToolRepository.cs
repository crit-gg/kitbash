namespace Kitbash.Tools;

/// <summary>
/// Somewhere tools are published. Two operations and no more: list the versions this
/// repository offers, and fetch a named file for one of them. Everything the launcher
/// does is built out of those, so adding a repository type stays small.
/// </summary>
public interface IToolRepository
{
    ToolRepositorySource Source { get; }

    /// <param name="refresh">Ignores anything cached and asks now.</param>
    /// <exception cref="ToolRepositoryException">It could not be reached or understood.</exception>
    Task<IReadOnlyList<ToolRelease>> ListAsync(bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// One asset as text. For the manifest, which is a few hundred bytes. A payload goes
    /// to a file instead, since it is too large to hold.
    /// </summary>
    /// <exception cref="ToolRepositoryException">It is absent or could not be read.</exception>
    Task<string> ReadTextAsync(ToolRelease release, string asset, CancellationToken cancellationToken);

    /// <summary>
    /// The same fetch, to a file, for a payload too large to hold as text. Progress is
    /// bytes written. Nothing else is created, so the caller does its own renaming.
    /// </summary>
    /// <exception cref="ToolRepositoryException">It is absent or could not be fetched.</exception>
    Task FetchAsync(
        ToolRelease release,
        string asset,
        string path,
        IProgress<long>? progress,
        CancellationToken cancellationToken);
}
