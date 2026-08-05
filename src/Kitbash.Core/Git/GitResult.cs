namespace Kitbash.Core.Git;

/// <summary>How far a run of git got, before anything it said is read.</summary>
public enum GitRunOutcome
{
    /// <summary>Git ran and exited. The exit code says whether it liked the request.</summary>
    Ran,

    /// <summary>There is no git on this machine, or the folder is not there.</summary>
    Unavailable,

    /// <summary>Git was still going when the limit ran out, and was stopped.</summary>
    TimedOut,

    /// <summary>Git could not be started at all.</summary>
    DidNotStart,
}

/// <summary>What one run of git left behind.</summary>
public sealed record GitResult(GitRunOutcome Outcome, int ExitCode, string Output, string Error)
{
    public static GitResult Nothing(GitRunOutcome outcome) => new(outcome, -1, "", "");

    /// <summary>Nothing needed doing, which counts as having worked.</summary>
    public static GitResult Skipped { get; } = new(GitRunOutcome.Ran, 0, "", "");

    /// <summary>Git ran and said it worked.</summary>
    public bool Succeeded => Outcome == GitRunOutcome.Ran && ExitCode == 0;

    /// <summary>Output split on newlines, with the trailing blank one dropped.</summary>
    public IReadOnlyList<string> Lines =>
        Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

    /// <summary>
    /// Output split on the null byte, for the <c>-z</c> forms. Those are the only forms safe
    /// for paths, since git otherwise quotes anything with a space or a newline in it.
    /// </summary>
    public IReadOnlyList<string> Records =>
        Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Git's own words about what happened, trimmed. It reports on itself through error even
    /// when it succeeded, so output is only read when error had nothing.
    /// </summary>
    public string Message => Error.Trim() is { Length: > 0 } said ? said : Output.Trim();
}
