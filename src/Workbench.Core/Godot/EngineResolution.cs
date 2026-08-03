namespace Workbench.Core.Godot;

/// <summary>How well this machine answers what a workspace asked for.</summary>
public enum EngineMatch
{
    /// <summary>There is no Godot project here, so there is nothing to answer.</summary>
    None,

    /// <summary>An installed engine is the one to use and it is the one asked for.</summary>
    Matched,

    /// <summary>
    /// An engine will be used and it is not the one asked for. The version is wrong, or
    /// the version is right and the .NET flag is not.
    /// </summary>
    Mismatch,

    /// <summary>Nothing installed can be used at all.</summary>
    Missing,
}

/// <summary>
/// What a workspace asked for, and what this machine can do about it.
/// </summary>
/// <param name="Requirement">What was asked, and where it was read from.</param>
/// <param name="Engine">
/// The engine that would open this project, or null when there is none to offer.
/// </param>
/// <param name="Match">How well the two agree.</param>
/// <param name="IsDefault">
/// True when <paramref name="Engine"/> is this machine's default rather than something
/// the workspace named. The two are otherwise indistinguishable to a caller.
/// </param>
public sealed record EngineResolution(
    EngineRequirement Requirement,
    InstalledEngine? Engine,
    EngineMatch Match,
    bool IsDefault)
{
    public static EngineResolution None { get; } =
        new(EngineRequirement.None, null, EngineMatch.None, IsDefault: false);

    public bool HasEngine => Engine is not null;
}
