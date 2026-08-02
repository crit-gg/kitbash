using Workbench.Core.Platform;

namespace Workbench.Core.Godot;

/// <summary>
/// One Godot release as the version feed states it.
/// </summary>
/// <remarks>
/// This is the fact and not the files. What a release holds is read from its manifest,
/// through <see cref="EngineBuild.ReadAll"/>, since the feed says nothing about files and
/// deriving their names is what the landscape sweep ruled out.
/// </remarks>
/// <param name="Tag">The release tag, which is also the folder in every download URL.</param>
/// <param name="Released">
/// The release date. The feed writes it as prose such as "21 July 2026", so it is parsed
/// with an invariant culture. Measured: all 356 dates in the feed parse.
/// </param>
/// <param name="Notes">
/// The article about this release, or null when the feed carries none. Measured: one
/// release in the feed has an empty notes URL, so this is read as optional.
/// </param>
public sealed record EngineRelease(EngineTag Tag, DateOnly Released, WebAddress? Notes)
{
    public EngineChannel Channel => Tag.Channel;

    public bool IsStable => Tag.Channel == EngineChannel.Stable;
}
