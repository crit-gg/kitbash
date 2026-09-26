namespace Kitbash.Core.Godot;

/// <summary>
/// The release list, and how much to trust it.
/// </summary>
/// <param name="Releases">Godot 4 and later, newest first.</param>
/// <param name="ReadAt">When the copy behind this was fetched, not when it was read here.</param>
/// <param name="IsStale">
/// True when the network could not be reached and this is the cached copy. The page draws
/// that rather than failing, since a list from yesterday is worth more than an error.
/// </param>
public sealed record EngineReleases(
    IReadOnlyList<EngineRelease> Releases,
    DateTimeOffset ReadAt,
    bool IsStale)
{
    /// <summary>
    /// Releases a repository published whose tag could not be read, so they are not
    /// listed. Always zero for the official list.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// The highest stable version, or null when the list holds none.
    /// </summary>
    public EngineRelease? NewestStable =>
        Releases.Where(release => release.IsStable).MaxBy(release => release.Tag);
}
