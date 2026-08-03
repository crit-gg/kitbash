namespace Kitbash.Core.Git;

/// <summary>What came back from asking a repository to update itself.</summary>
public enum GitUpdateOutcome
{
    /// <summary>Fetched, and left the branch where it was.</summary>
    Fetched,

    /// <summary>Fetched, and the branch caught up with its upstream.</summary>
    Pulled,

    /// <summary>There is nothing to fetch from. A local only repository has no remote.</summary>
    NoRemote,

    /// <summary>Git wanted a password, a passphrase or a host key answer, and there is nowhere to ask.</summary>
    NeedsCredentials,

    /// <summary>The work took too long and was stopped.</summary>
    TimedOut,

    /// <summary>Git ran and failed. Offline, a rejected key, a missing branch, anything else.</summary>
    Failed,
}

/// <summary>What an update did, and what git said about it.</summary>
/// <param name="Message">Git's own words, trimmed. Empty when it had none.</param>
public sealed record GitUpdateResult(GitUpdateOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome is GitUpdateOutcome.Fetched or GitUpdateOutcome.Pulled;
}

/// <summary>Brings a repository up to date with its remote.</summary>
public interface IGitUpdater
{
    /// <summary>
    /// Fetches, then takes the new commits when taking them cannot cost anything.
    /// </summary>
    Task<GitUpdateResult> UpdateAsync(string root, CancellationToken cancellation = default);
}
