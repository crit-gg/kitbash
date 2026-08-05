using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>Reads and settles conflicts by running git.</summary>
public sealed class GitConflictReader : IGitConflictReader
{
    private readonly IGitRunner _git;

    public GitConflictReader(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<IReadOnlyList<GitConflict>> ReadAsync(
        string root, CancellationToken cancellation = default)
    {
        var result = await _git
            .RunAsync(root, GitCommand.Of("ls-files", "--unmerged", "-z"), cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return [];
        }

        // One record per version, so a path with three of them arrives three times and the
        // versions are gathered under it.
        var found = new Dictionary<string, GitConflictVersion?[]>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var record in result.Records)
        {
            var tab = record.IndexOf('\t', StringComparison.Ordinal);

            if (tab < 0)
            {
                continue;
            }

            var fields = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 3
                || !int.TryParse(fields[2], CultureInfo.InvariantCulture, out var stage)
                || stage is < 1 or > 3)
            {
                continue;
            }

            var path = record[(tab + 1)..];

            if (!found.TryGetValue(path, out var versions))
            {
                found[path] = versions = new GitConflictVersion?[4];
                order.Add(path);
            }

            versions[stage] = new GitConflictVersion((GitConflictSide)stage, fields[0], fields[1]);
        }

        return
        [
            .. order.Select(path => new GitConflict(
                path, found[path][1], found[path][2], found[path][3])),
        ];
    }

    public async Task<GitMergeState> ReadStateAsync(
        string root, CancellationToken cancellation = default)
    {
        // A rebase is checked first. Its sequencer also writes CHERRY_PICK_HEAD, so asking
        // about that one first would call every stopped rebase a cherry pick.
        foreach (var (head, state) in Heads)
        {
            var result = await _git
                .RunAsync(root, GitCommand.Of("rev-parse", "--quiet", "--verify", head), cancellation)
                .ConfigureAwait(false);

            if (result.Succeeded)
            {
                return state;
            }
        }

        return GitMergeState.None;
    }

    private static (string Head, GitMergeState State)[] Heads =>
    [
        ("REBASE_HEAD", GitMergeState.Rebasing),
        ("MERGE_HEAD", GitMergeState.Merging),
        ("CHERRY_PICK_HEAD", GitMergeState.CherryPicking),
        ("REVERT_HEAD", GitMergeState.Reverting),
    ];

    public async Task<string?> ReadVersionAsync(
        string root,
        GitConflict conflict,
        GitConflictSide side,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var version = side switch
        {
            GitConflictSide.Base => conflict.Base,
            GitConflictSide.Ours => conflict.Ours,
            _ => conflict.Theirs,
        };

        if (version is null)
        {
            return null;
        }

        var result = await _git
            .RunAsync(root, GitCommand.Of("cat-file", "blob", version.ObjectId), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? result.Output : null;
    }

    public Task<GitResult> ResolveAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0)
        {
            return Task.FromResult(GitResult.Skipped);
        }

        var arguments = new List<string> { "add", "--" };
        arguments.AddRange(paths);

        return _git.RunAsync(root, new GitCommand(arguments), cancellation);
    }

    public async Task<GitResult> TakeAsync(
        string root,
        IReadOnlyList<string> paths,
        GitConflictSide side,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0 || side == GitConflictSide.Base)
        {
            return GitResult.Skipped;
        }

        var arguments = new List<string>
        {
            "checkout",
            side == GitConflictSide.Ours ? "--ours" : "--theirs",
            "--",
        };

        arguments.AddRange(paths);

        var taken = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        // Taking a side writes the working tree. The index still holds three versions until
        // the path is added, and git counts it as conflicted until then.
        return taken.Succeeded
            ? await ResolveAsync(root, paths, cancellation).ConfigureAwait(false)
            : taken;
    }

    public async Task<GitResult> AbortAsync(string root, CancellationToken cancellation = default)
    {
        var command = await ReadStateAsync(root, cancellation).ConfigureAwait(false) switch
        {
            GitMergeState.Merging => "merge",
            GitMergeState.Rebasing => "rebase",
            GitMergeState.CherryPicking => "cherry-pick",
            GitMergeState.Reverting => "revert",
            _ => null,
        };

        return command is null
            ? GitResult.Skipped
            : await _git.RunAsync(root, GitCommand.Of(command, "--abort"), cancellation)
                .ConfigureAwait(false);
    }
}
