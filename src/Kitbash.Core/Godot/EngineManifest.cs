namespace Kitbash.Core.Godot;

/// <summary>
/// What one release actually published, read from its manifest in <c>godot-builds</c> or
/// from a repository release's assets.
/// </summary>
/// <param name="Tag">The release this describes.</param>
/// <param name="Builds">Every desktop editor in it, for every platform, in a stable order.</param>
/// <param name="Checksums">The hash of each file by file name, lower case hex.</param>
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
    /// <summary>Which hash <see cref="Checksums"/> holds.</summary>
    public EngineChecksumKind ChecksumKind { get; init; } = EngineChecksumKind.Sha512;

    /// <summary>The export templates in this release, at most one per runtime.</summary>
    public IReadOnlyList<EngineTemplatesFile> Templates { get; init; } = [];

    /// <summary>
    /// The published hash for one build, or null when the release predates checksums.
    /// A caller that cannot find one refuses to install rather than shrugging.
    /// </summary>
    public string? ChecksumFor(EngineBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        return Checksums.GetValueOrDefault(build.FileName);
    }

    /// <summary>The hash for any file this release published, by name.</summary>
    public string? ChecksumFor(string fileName) => Checksums.GetValueOrDefault(fileName);

    /// <summary>The templates for one runtime, or null when the release published none.</summary>
    public EngineTemplatesFile? TemplatesFor(bool mono) => Templates.FirstOrDefault(file => file.IsMono == mono);

    /// <summary>Every build for one platform, which is how a card is filled.</summary>
    public IReadOnlyList<EngineBuild> For(EnginePlatform platform) =>
        Builds.Where(b => b.Platform == platform).ToList();
}
