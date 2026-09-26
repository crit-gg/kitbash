using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// One Godot release as the version feed or an engine repository states it.
/// </summary>
/// <param name="Tag">
/// The release tag for an official release, which is also the folder in every download
/// URL. For a repository release, the base version with the custom channel.
/// </param>
/// <param name="Released">
/// The release date. The feed writes it as prose such as "21 July 2026", so it is parsed
/// with an invariant culture.
/// </param>
/// <param name="Notes">
/// The article about this release, or null. A feed entry may carry an empty notes URL.
/// </param>
public sealed record EngineRelease(EngineTag Tag, DateOnly Released, WebAddress? Notes)
{
    /// <summary>Null for a release the Godot project published.</summary>
    public EngineRepositoryAddress? Repository { get; init; }

    /// <summary>
    /// The tag exactly as published, such as <c>4.7.2-slopworks-18d5d19</c>. Every file
    /// name in the release starts with it.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The last part of a repository's tag. Null for an official release.</summary>
    public string? Build { get; init; }

    /// <summary>Marked prerelease where it was published. A newest pin never moves to one.</summary>
    public bool IsPrerelease { get; init; }

    /// <summary>
    /// When it was published, to the second where the source says. Orders the builds of
    /// one version inside a repository, since a build is not orderable.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }

    public EngineChannel Channel => Tag.Channel;

    public bool IsStable => Tag.Channel == EngineChannel.Stable;

    /// <summary>What every file name in this release starts with, after <c>Godot_v</c>.</summary>
    public string FileTag => Repository is null ? Tag.ToString() : Name;

    /// <summary>Unique across every repository and the official list.</summary>
    public string Key => Repository is null ? Tag.ToString() : $"{Repository}/{Name}";

    /// <summary>The install one of this release's builds would be, before its runtime is known.</summary>
    public EngineId IdFor(bool mono) => new(Tag, mono) { Repository = Repository, Build = Build };
}
