using Workbench.Core.IO;
using Workbench.Core.Platform;

namespace Workbench.Core.Git;

/// <summary>
/// Runs <c>git fetch</c>, and then takes the new commits when taking them cannot cost
/// anything.
/// </summary>
/// <remarks>
/// This is the one thing here that touches a network, and the one thing that writes to a
/// person's repository. Both are guarded.
/// <para>
/// <b>Nothing may prompt.</b> Git will happily stop and wait for a password, a passphrase or
/// an answer about a host key, and there is no terminal behind this to type into, so it
/// would wait until the app closed. The variables below turn every one of those into an
/// error. <c>GIT_TERMINAL_PROMPT</c> covers git's own asking, <c>GIT_ASKPASS</c> and
/// <c>SSH_ASKPASS</c> stop a graphical helper being launched, and the ssh options refuse an
/// unknown host rather than asking about it. A credential helper the person has already set
/// up still works, because it answers without asking. That is the case worth keeping: an
/// update that just works for anyone whose terminal already does.
/// </para>
/// <para>
/// <b>There is still a timeout</b>, because a network can accept a connection and then say
/// nothing. Cancelling the wait kills git, so a slow update costs one stopped process rather
/// than one that runs forever.
/// </para>
/// <para>
/// <b>Nothing local is ever at risk.</b> Taking the commits is refused unless every one of
/// these holds, read after the fetch rather than before it:
/// </para>
/// <list type="bullet">
/// <item>the working tree and the index are clean, so there is nothing of the person's to
/// overwrite, stash or conflict with</item>
/// <item>the head is a branch and not a commit, since a detached head has nothing to
/// advance</item>
/// <item>the branch tracks something</item>
/// <item>the branch is behind, so there is a reason to</item>
/// <item>the branch is not also ahead, so this is a straight line and not two histories</item>
/// </list>
/// <para>
/// And then <c>--ff-only</c> on top, which is the guarantee rather than the check. Even if
/// every count above were stale, git itself will only move the branch pointer forward. It
/// cannot merge, cannot rebase, cannot commit and cannot rewrite anything. When it cannot do
/// that it does nothing and says so.
/// </para>
/// <para>
/// It is a merge against the upstream ref rather than a second <c>git pull</c>, because the
/// fetch just above already brought everything down. A pull here would go back to the
/// network to learn what it already knows.
/// </para>
/// </remarks>
public sealed class GitUpdater : IGitUpdater
{
    private const string Program = "git";

    /// <summary>Long enough for a large repository on a slow link, short enough to give up on.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Everything that would otherwise wait for a person. An empty value unsets the
    /// variable, which is what stops a helper that the environment already names.
    /// </summary>
    private static readonly Dictionary<string, string> Unattended = new(StringComparer.Ordinal)
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_ASKPASS"] = "",
        ["SSH_ASKPASS"] = "",
        ["SSH_ASKPASS_REQUIRE"] = "never",
        ["GIT_SSH_COMMAND"] = "ssh -oBatchMode=yes -oStrictHostKeyChecking=accept-new",
        ["GCM_INTERACTIVE"] = "never",
    };

    private readonly IProcessRunner _processes;
    private readonly IExecutableFinder _executables;
    private readonly IFileSystem _fileSystem;
    private readonly IGitStatusReader _status;

    public GitUpdater(
        IProcessRunner processes,
        IExecutableFinder executables,
        IFileSystem fileSystem,
        IGitStatusReader status)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(executables);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(status);

        _processes = processes;
        _executables = executables;
        _fileSystem = fileSystem;
        _status = status;
    }

    public async Task<GitUpdateResult> UpdateAsync(string root, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (_executables.Find(Program) is not { } git || !_fileSystem.DirectoryExists(root))
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
                ProcessRequest.CommandIn(root, git, arguments).With(Unattended),
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
