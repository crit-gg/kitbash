using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// One Godot release as the version feed states it.
/// </summary>
/// <param name="Tag">The release tag, which is also the folder in every download URL.</param>
/// <param name="Released">
/// The release date. The feed writes it as prose such as "21 July 2026", so it is parsed
/// with an invariant culture.
/// </param>
/// <param name="Notes">
/// The article about this release, or null. A feed entry may carry an empty notes URL.
/// </param>
public sealed record EngineRelease(EngineTag Tag, DateOnly Released, WebAddress? Notes)
{
    public EngineChannel Channel => Tag.Channel;

    public bool IsStable => Tag.Channel == EngineChannel.Stable;
}
