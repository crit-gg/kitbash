namespace Kitbash.Core.Git;

/// <summary>
/// Pushes and pulls by running git, and reads out of git's words which of the outcomes it
/// was. Git says why in prose rather than in an exit code, so prose is what there is to read.
/// </summary>
public sealed class GitSync : IGitSync
{
    private readonly IGitRunner _git;

    public GitSync(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<GitSyncResult> FetchAsync(
        string root, string? remote = null, CancellationToken cancellation = default)
    {
        var arguments = new List<string> { "fetch", "--prune" };

        if (remote is { Length: > 0 } named)
        {
            arguments.Add(named);
        }
        else
        {
            arguments.Add("--all");
        }

        var result = await _git
            .RunAsync(root, new GitCommand(arguments).OverTheNetwork(), cancellation)
            .ConfigureAwait(false);

        return Read(result, GitSyncOutcome.Done);
    }

    public async Task<GitSyncResult> PushAsync(
        string root, GitPushRequest? request = null, CancellationToken cancellation = default)
    {
        var settings = request ?? GitPushRequest.Default;

        var arguments = new List<string> { "push" };

        if (settings.SetUpstream)
        {
            arguments.Add("--set-upstream");
        }

        if (settings.ForceWithLease)
        {
            arguments.Add("--force-with-lease");
        }

        if (settings.WithTags)
        {
            arguments.Add("--follow-tags");
        }

        if (settings.Remote is { Length: > 0 } remote)
        {
            arguments.Add(remote);
            arguments.Add(settings.Branch is { Length: > 0 } branch ? branch : "HEAD");
        }

        var result = await _git
            .RunAsync(root, new GitCommand(arguments).OverTheNetwork(), cancellation)
            .ConfigureAwait(false);

        return Read(result, GitSyncOutcome.Done);
    }

    public async Task<GitSyncResult> PullAsync(
        string root, GitPullRequest? request = null, CancellationToken cancellation = default)
    {
        var settings = request ?? GitPullRequest.Default;

        var fetched = await FetchAsync(root, settings.Remote, cancellation).ConfigureAwait(false);

        if (!fetched.Succeeded)
        {
            return fetched;
        }

        var arguments = new List<string> { "merge" };

        if (settings.FastForwardOnly)
        {
            arguments.Add("--ff-only");
        }

        // The fetch just above brought everything down, so this reads what is already here
        // rather than going back to the network to learn what it knows.
        arguments.Add(Target(settings));

        var merged = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return Read(merged, GitSyncOutcome.Done);
    }

    /// <summary>What to merge, which is the upstream unless the caller named something.</summary>
    private static string Target(GitPullRequest settings)
    {
        if (settings.Remote is { Length: > 0 } remote && settings.Branch is { Length: > 0 } branch)
        {
            return $"{remote}/{branch}";
        }

        return "@{u}";
    }

    private static GitSyncResult Read(GitResult result, GitSyncOutcome done)
    {
        switch (result.Outcome)
        {
            case GitRunOutcome.TimedOut:
                return new GitSyncResult(GitSyncOutcome.TimedOut, "");
            case GitRunOutcome.Unavailable:
            case GitRunOutcome.DidNotStart:
                return new GitSyncResult(GitSyncOutcome.Failed, "");
        }

        var message = result.Message;

        if (result.Succeeded)
        {
            return new GitSyncResult(
                message.Contains("Everything up-to-date", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Already up to date", StringComparison.OrdinalIgnoreCase)
                    ? GitSyncOutcome.AlreadyLevel
                    : done,
                message);
        }

        return new GitSyncResult(Outcome(message), message);
    }

    private static GitSyncOutcome Outcome(string message)
    {
        if (Says(message,
            "could not read Username",
            "could not read Password",
            "terminal prompts disabled",
            "Authentication failed",
            "Permission denied",
            "Host key verification failed"))
        {
            return GitSyncOutcome.NeedsCredentials;
        }

        if (Says(message, "has no upstream branch", "no upstream configured", "no tracking information"))
        {
            return GitSyncOutcome.NoUpstream;
        }

        if (Says(message,
            "does not appear to be a git repository",
            "No remote repository specified",
            "No configured push destination"))
        {
            return GitSyncOutcome.NoRemote;
        }

        if (Says(message, "non-fast-forward", "fetch first", "[rejected]"))
        {
            return GitSyncOutcome.Rejected;
        }

        if (Says(message, "Automatic merge failed", "CONFLICT ("))
        {
            return GitSyncOutcome.Conflicted;
        }

        if (Says(message,
            "would be overwritten by merge",
            "local changes to the following files",
            "You have unstaged changes",
            "Please commit your changes or stash them"))
        {
            return GitSyncOutcome.Blocked;
        }

        // A refused fast forward reads as diverged, which is the one thing left it can be
        // once the reasons above are ruled out.
        if (Says(message, "Not possible to fast-forward", "divergent branches", "refusing to merge unrelated histories"))
        {
            return GitSyncOutcome.Diverged;
        }

        return GitSyncOutcome.Failed;
    }

    private static bool Says(string message, params string[] phrases) =>
        phrases.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
}
