namespace Kitbash.Core.Godot;

/// <summary>
/// What one release actually published, read from its manifest in <c>godot-builds</c>.
/// </summary>
/// <param name="Tag">The release this describes.</param>
/// <param name="Builds">Every desktop editor in it, for every platform, in a stable order.</param>
/// <param name="Checksums">SHA 512 by file name, lower case hex, as the project publishes it.</param>
/// <param name="PublishedTargets">
/// Every target the release shipped for, named for reading, including the ones Kitbash
/// cannot install. Empty is possible and means the release published no editor at all.
/// </param>
public sealed record EngineManifest(
    EngineTag Tag,
    IReadOnlyList<EngineBuild> Builds,
    IReadOnlyDictionary<string, string> Checksums,
    IReadOnlyList<string> PublishedTargets)
{
    /// <summary>
    /// The published hash for one build, or null when the release predates checksums.
    /// A caller that cannot find one refuses to install rather than shrugging.
    /// </summary>
    public string? ChecksumFor(EngineBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        return Checksums.GetValueOrDefault(build.FileName);
    }

    /// <summary>Every build for one platform, which is how a card is filled.</summary>
    public IReadOnlyList<EngineBuild> For(EnginePlatform platform) =>
        Builds.Where(b => b.Platform == platform).ToList();
}
