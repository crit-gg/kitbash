namespace Workbench.Core.Git;

/// <summary>How a clone ended.</summary>
public enum GitCloneOutcome
{
    Cloned = 0,

    /// <summary>There is no git on this machine, so nothing was run.</summary>
    GitMissing = 1,

    /// <summary>Something is already at the destination. Nothing was written.</summary>
    DestinationExists = 2,

    /// <summary>Git needed a username, a password or a key and there was nobody to ask.</summary>
    NeedsCredentials = 3,

    /// <summary>The address answered and there is no repository there.</summary>
    NotFound = 4,

    Cancelled = 5,

    TimedOut = 6,

    Failed = 7,
}

/// <summary>
/// What one clone did. <paramref name="Message"/> is git's own words, empty when git
/// never ran.
/// </summary>
public sealed record GitCloneResult(GitCloneOutcome Outcome, string Destination, string Message)
{
    public bool Succeeded => Outcome is GitCloneOutcome.Cloned;
}
