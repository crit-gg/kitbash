using System.Collections.Immutable;

namespace Kitbash.Core.Git;

/// <summary>
/// A merge driver as one repository would be told about it. Git runs the command with
/// <c>%O %A %B %L %P</c> substituted, and takes the file written to <c>%A</c> as the result.
/// </summary>
/// <param name="Name">The key under <c>merge</c> in the config, and what an attribute names.</param>
/// <param name="Title">What git prints when it mentions the driver.</param>
/// <param name="Command">The whole command line, placeholders included.</param>
/// <param name="Patterns">The path patterns this driver claims, such as <c>*.tscn</c>.</param>
public sealed record GitMergeDriver(
    string Name,
    string Title,
    string Command,
    ImmutableArray<string> Patterns);

/// <summary>What wiring a driver into a repository did.</summary>
public enum GitMergeDriverOutcome
{
    /// <summary>The repository already said exactly this, so nothing was written.</summary>
    Ready,

    /// <summary>The config or the attributes were written.</summary>
    Wired,

    /// <summary>Something else already claims a pattern, so it was left alone.</summary>
    Claimed,

    /// <summary>Git could not be run, or the repository would not take it.</summary>
    Failed,
}

/// <summary>
/// Tells one repository about a merge driver. Both halves live outside the working tree and
/// neither is cloned, so every clone has to be told again and a teammate without it merges
/// the ordinary way.
/// </summary>
public interface IGitMergeDrivers
{
    /// <summary>
    /// Writes the driver into this repository's own config and attributes, where they do not
    /// already say it. A pattern another driver already claims is left as it is.
    /// </summary>
    Task<GitMergeDriverOutcome> EnsureAsync(
        string root, GitMergeDriver driver, CancellationToken cancellation = default);
}
