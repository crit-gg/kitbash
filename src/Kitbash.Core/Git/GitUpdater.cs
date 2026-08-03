using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Git;

/// <summary>
/// Runs <c>git fetch</c>, and then takes the new commits when taking them cannot cost
/// anything.
/// </summary>
public sealed class GitUpdater : IGitUpdater
{
    /// <summary>Long enough for a large repository on a slow link, short enough to give up on.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly IFileSystem _fileSystem;
    private readonly IGitStatusReader _status;
    private readonly GitEnvironment _environment;

    public GitUpdater(
        IProcessRunner processes,
        IExternalTools tools,
        IFileSystem fileSystem,
        IGitStatusReader status,
        GitEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(environment);

        _processes = processes;
        _tools = tools;
        _fileSystem = fileSystem;
        _status = status;
        _environment = environment;
    }

    public async Task<GitUpdateResult> UpdateAsync(string root, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (_tools.Git.Path is not { } git || !_fileSystem.DirectoryExists(root))
        {
            return new GitUpdateResult(GitUpdateOutcome.Failed, "");
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(Limit);

        var fetched = await RunAsync(
            git, root, limit.Token, cancellation,
            "fetch", "--all", "--prune", "--quiet").ConfigureAwait(false);

        if (fetched.Outcome != GitUpdateOutcome.Fetched)
        {
            return fetched;
        }

        // Read after the fetch. Before it, behind is whatever it was the last time anyone
        // looked, which is exactly the number this decision must not be made on.
        if (await _status.ReadAsync(root, limit.Token).ConfigureAwait(false) is not { } status
            || !CanTake(status))
        {
            return fetched;
        }

        var pulled = await RunAsync(
            git, root, limit.Token, cancellation,
            "merge", "--ff-only", "--quiet", "@{u}").ConfigureAwait(false);

        // A refused fast forward is not a failed update. The fetch worked, the strip is
        // right, and the reason it would not move is something only a person can settle.
        return pulled.Outcome == GitUpdateOutcome.Fetched
            ? new GitUpdateResult(GitUpdateOutcome.Pulled, pulled.Message)
            : fetched;
    }

    /// <summary>
    /// Whether the new commits can be taken without costing the person anything. See the
    /// remarks on this class for why each of these is here.
    /// </summary>
    private static bool CanTake(GitStatus status) =>
        status.IsClean
        && !status.IsDetached
        && status.HasUpstream
        && status.Behind > 0
        && status.Ahead == 0;

    private async Task<GitUpdateResult> RunAsync(
        string git,
        string root,
        CancellationToken limit,
        CancellationToken cancellation,
        params string[] arguments)
    {
        ProcessOutput output;

        try
        {
            output = await _processes.ReadAsync(
                ProcessRequest.CommandIn(root, git, arguments).With(_environment.Unattended),
                limit).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return new GitUpdateResult(GitUpdateOutcome.TimedOut, "");
        }
        catch (ProcessStartException)
        {
            return new GitUpdateResult(GitUpdateOutcome.Failed, "");
        }

        var message = output.StandardError.Trim();

        return output.Succeeded
            ? new GitUpdateResult(GitUpdateOutcome.Fetched, message)
            : new GitUpdateResult(Read(message), message);
    }

    // Git says why in words rather than in an exit code, so the words are what there is to
    // read. Anything unrecognised is a plain failure, which is the safe way round.
    private static GitUpdateOutcome Read(string message)
    {
        if (message.Contains("does not appear to be a git repository", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No remote repository specified", StringComparison.OrdinalIgnoreCase))
        {
            return GitUpdateOutcome.NoRemote;
        }

        if (message.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not read Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
        {
            return GitUpdateOutcome.NeedsCredentials;
        }

        return GitUpdateOutcome.Failed;
    }
}
