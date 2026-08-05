namespace Kitbash.Core.Git;

/// <summary>
/// Merges by running git, and reads out of git's words which of the outcomes it was.
/// </summary>
public sealed class GitMerger : IGitMerger
{
    private readonly IGitRunner _git;
    private readonly IGitRefReader _refs;

    public GitMerger(IGitRunner git, IGitRefReader refs)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(refs);

        _git = git;
        _refs = refs;
    }

    public async Task<GitMergePreview> PreviewAsync(
        string root,
        string theirs,
        string ours = "HEAD",
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(theirs);
        ArgumentException.ThrowIfNullOrWhiteSpace(ours);

        // Without a merge base named, merge-tree finds one itself and refuses when the two
        // share no history. Naming it keeps that case answerable rather than an error.
        var start = await _refs.ReadMergeBaseAsync(root, ours, theirs, cancellation)
            .ConfigureAwait(false);

        if (start is null)
        {
            return GitMergePreview.Unknown;
        }

        var result = await _git.RunAsync(
            root,
            GitCommand.Of(
                "merge-tree", "--write-tree", "--name-only", "-z", "--merge-base=" + start, ours, theirs),
            cancellation).ConfigureAwait(false);

        if (result.Outcome != GitRunOutcome.Ran)
        {
            return GitMergePreview.Unknown;
        }

        // Zero is a clean merge and one is conflicts. Anything else is git refusing the
        // request, such as a revision that does not resolve.
        if (result.ExitCode == 0)
        {
            return new GitMergePreview(GitMergeVerdict.Clean, []);
        }

        if (result.ExitCode != 1)
        {
            return GitMergePreview.Unknown;
        }

        return new GitMergePreview(GitMergeVerdict.Conflicts, Conflicted(result.Output));
    }

    public async Task<GitMergeResult> MergeAsync(
        string root,
        string revision,
        GitMergeRequest? request = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var settings = request ?? GitMergeRequest.Default;

        var arguments = new List<string> { "merge" };

        if (settings.FastForwardOnly)
        {
            arguments.Add("--ff-only");
        }

        if (settings.NoFastForward)
        {
            arguments.Add("--no-ff");
        }

        if (settings.Message is { Length: > 0 } message)
        {
            arguments.Add("--message");
            arguments.Add(message);
        }
        else
        {
            arguments.Add("--no-edit");
        }

        arguments.Add(revision);

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return new GitMergeResult(Outcome(result), result.Message);
    }

    public async Task<bool> IsAncestorAsync(
        string root,
        string ancestor,
        string descendant,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ancestor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descendant);

        var result = await _git.RunAsync(
            root,
            GitCommand.Of("merge-base", "--is-ancestor", ancestor, descendant),
            cancellation).ConfigureAwait(false);

        return result.Succeeded;
    }

    /// <summary>
    /// Reads the conflicted paths out of merge-tree's null separated form. The tree comes
    /// first, then the paths, then an empty record before git's own messages.
    /// </summary>
    private static IReadOnlyList<string> Conflicted(string output)
    {
        var records = output.Split('\0');
        var paths = new List<string>();

        for (var index = 1; index < records.Length && records[index].Length > 0; index++)
        {
            paths.Add(records[index]);
        }

        return paths;
    }

    private static GitMergeOutcome Outcome(GitResult result)
    {
        switch (result.Outcome)
        {
            case GitRunOutcome.TimedOut:
                return GitMergeOutcome.TimedOut;
            case GitRunOutcome.Unavailable:
            case GitRunOutcome.DidNotStart:
                return GitMergeOutcome.Failed;
        }

        var message = result.Message;

        if (result.Succeeded)
        {
            if (Says(message, "Already up to date"))
            {
                return GitMergeOutcome.AlreadyLevel;
            }

            return Says(message, "Fast-forward")
                ? GitMergeOutcome.FastForwarded
                : GitMergeOutcome.Merged;
        }

        if (Says(message, "not something we can merge"))
        {
            return GitMergeOutcome.NoSuchRevision;
        }

        if (Says(message, "Not possible to fast-forward"))
        {
            return GitMergeOutcome.NotFastForward;
        }

        if (Says(message, "would be overwritten by merge")
            || Says(message, "Please commit your changes or stash them"))
        {
            return GitMergeOutcome.Blocked;
        }

        return Says(message, "Automatic merge failed") || Says(message, "CONFLICT")
            ? GitMergeOutcome.Conflicted
            : GitMergeOutcome.Failed;
    }

    private static bool Says(string message, string phrase) =>
        message.Contains(phrase, StringComparison.OrdinalIgnoreCase);
}
