namespace Kitbash.Core.Git;

/// <summary>One run of git, described so the runner can add what every run needs.</summary>
public sealed record GitCommand(IReadOnlyList<string> Arguments)
{
    /// <summary>Long enough for a large repository, short enough that a wedged git gives up.</summary>
    public static readonly TimeSpan LocalLimit = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A backstop rather than a wait anybody should reach. Talking to a remote is cancelled
    /// by the person, so the caller's own token is what normally stops it.
    /// </summary>
    public static readonly TimeSpan NetworkLimit = TimeSpan.FromMinutes(30);

    public static GitCommand Of(params string[] arguments) => new(arguments);

    /// <summary>
    /// Text handed to git's input. Only for content that cannot go in an argument, such as a
    /// patch or a commit message, since every operating system caps a command line.
    /// </summary>
    public string? StandardInput { get; init; }

    /// <summary>
    /// True when git may talk to a remote, which sets the variables that stop it waiting for
    /// a person and lengthens the limit.
    /// </summary>
    public bool Network { get; init; }

    public TimeSpan Limit { get; init; } = LocalLimit;

    public GitCommand Reading(string input) => this with { StandardInput = input };

    /// <summary>
    /// The same, with the paths handed over through git's input rather than as arguments.
    /// Every operating system caps a command line and a selection can be any size, so a
    /// gesture over enough files would otherwise fail on how it was spelled. Not every
    /// subcommand takes these, and git clean is the one that does not.
    /// </summary>
    public GitCommand Over(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var arguments = new List<string>(Arguments)
        {
            "--pathspec-from-file=-",

            // Separated by the null byte, which is the only separator a path cannot hold.
            "--pathspec-file-nul",
        };

        return this with
        {
            Arguments = arguments,
            StandardInput = string.Concat(paths.Select(p => p + '\0')),
        };
    }

    public GitCommand OverTheNetwork() => this with { Network = true, Limit = NetworkLimit };

    public GitCommand Within(TimeSpan limit) => this with { Limit = limit };
}
