namespace Kitbash.Core.Platform;

/// <summary>Whether a terminal finds the command folder, and what it runs instead.</summary>
/// <param name="Shadow">
/// Another program of the same name that PATH reaches first, or null when there is none.
/// </param>
public sealed record CommandReach(PathReach Path, string? Shadow)
{
    public static CommandReach Unknown { get; } = new(PathReach.NotOnPath, null);
}

/// <summary>Where the command folder stands against PATH.</summary>
public enum PathReach
{
    /// <summary>A new terminal already searches it.</summary>
    OnPath,

    /// <summary>Put on PATH by this call, so only a terminal opened from now on has it.</summary>
    Added,

    /// <summary>Not on PATH, and nothing here may change that.</summary>
    NotOnPath,

    /// <summary>Not on PATH, because the person refused when asked.</summary>
    Declined,
}
