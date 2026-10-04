using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>Where the godot command stands after a sync. The app writes the words.</summary>
/// <param name="Command">Where the command is, or would be.</param>
/// <param name="Engine">The engine it runs, when it was written.</param>
/// <param name="Reach">Whether a terminal finds it, when it was written.</param>
/// <param name="Failure">What the system said, when writing failed.</param>
public sealed record EngineCommandState(
    EngineCommandKind Kind,
    string? Command = null,
    InstalledEngine? Engine = null,
    CommandReach? Reach = null,
    string? Failure = null);

public enum EngineCommandKind
{
    /// <summary>Nobody asked for it, and anything Kitbash wrote has been taken back.</summary>
    Off,

    /// <summary>This machine or this copy has nowhere to put it.</summary>
    Unavailable,

    /// <summary>There is no default engine to run.</summary>
    NoDefault,

    /// <summary>It runs the default engine.</summary>
    Placed,

    /// <summary>Something Kitbash did not write has the name, and it was left alone.</summary>
    Conflict,

    /// <summary>The folder or the entry could not be written.</summary>
    Failed,
}
