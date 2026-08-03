namespace Workbench.Core.Godot;

/// <summary>
/// What Godot has published. Reads the version feed and the per release manifests, caches
/// both, and never touches the GitHub API.
/// </summary>
public interface IEngineCatalogue
{
    /// <summary>
    /// Every release Workbench offers, newest first. Answers from cache while it is fresh,
    /// and answers from a stale cache rather than failing when the network is gone.
    /// </summary>
    /// <param name="refresh">Ignore a fresh cache and go to the network.</param>
    Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// What one release published. Cached without an expiry, since a tag names a fixed set
    /// of files once it is cut.
    /// </summary>
    Task<EngineManifest> ReadManifestAsync(EngineTag tag, CancellationToken cancellationToken);

    /// <summary>
    /// No build size is offered. Reading one costs a request per file, hundreds across
    /// every Godot 4 release, and nothing draws it.
    /// </summary>
}