namespace Workbench.Core.Godot;

/// <summary>
/// What Godot has published. Reads the version feed and the per release manifests, caches
/// both, and never touches the GitHub API.
/// </summary>
/// <remarks>
/// The API would answer all of this in one call and it is deliberately not used. Measured:
/// unauthenticated requests are limited to 60 an hour per address, and that counts every
/// process behind it, including the person's browser. gdvm hit that wall and moved off.
/// </remarks>
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
    /// How large one build is, or null when the server will not say. The feed carries no
    /// sizes and neither does a manifest, so this is a request of its own and the only
    /// reason a card touches the network when it opens.
    /// </summary>
    Task<long?> MeasureAsync(EngineBuild build, CancellationToken cancellationToken);
}
