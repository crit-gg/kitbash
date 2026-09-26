namespace Kitbash.Core.Godot;

/// <summary>
/// Somewhere engine builds are published. Two operations and no more: list the releases,
/// and read what one release holds. The official Godot list is one of these.
/// </summary>
public interface IEngineRepository
{
    /// <summary>Null for the official Godot list.</summary>
    EngineRepositoryAddress? Address { get; }

    /// <summary>
    /// Every release, newest first. Answers from cache while it is fresh, and from a stale
    /// cache rather than failing when the network is gone.
    /// </summary>
    /// <param name="refresh">Ignore a fresh cache and go to the network.</param>
    /// <exception cref="EngineCatalogueException">Nothing could be read and nothing was cached.</exception>
    Task<EngineReleases> ReadReleasesAsync(bool refresh, CancellationToken cancellationToken);

    /// <summary>What one release published, named as <see cref="EngineRelease.Name"/> names it.</summary>
    /// <exception cref="EngineCatalogueException">It could not be read.</exception>
    Task<EngineManifest> ReadManifestAsync(string release, CancellationToken cancellationToken);
}
