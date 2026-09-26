namespace Kitbash.Core.Godot;

/// <summary>What checking a newest pin came to.</summary>
public enum EngineUpdateOutcome
{
    /// <summary>The requirement is not a newest pin, so there is nothing to follow.</summary>
    NotFollowing,

    /// <summary>The slot is empty. Installing it is a person's choice, not a check's.</summary>
    NotInstalled,

    /// <summary>The slot already holds the newest build.</summary>
    UpToDate,

    /// <summary>A newer build replaced the one in the slot.</summary>
    Updated,

    /// <summary>A newer build is unpacked and waits for the running engine to close.</summary>
    Waiting,
}

/// <summary>
/// The result of one check.
/// </summary>
/// <param name="Engine">What the slot holds now, or null when it holds nothing.</param>
/// <param name="TemplatesFailure">
/// Why the templates did not follow the new build, or null when they did or were never installed.
/// </param>
public sealed record EngineUpdate(
    EngineUpdateOutcome Outcome,
    InstalledEngine? Engine = null,
    string? TemplatesFailure = null)
{
    public static EngineUpdate NotFollowing { get; } = new(EngineUpdateOutcome.NotFollowing);
}
