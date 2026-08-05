using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>Answers questions about refs by running git.</summary>
public sealed class GitRefReader : IGitRefReader
{
    private readonly IGitRunner _git;

    public GitRefReader(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<string?> ReadDefaultBranchAsync(
        string root, string remote = "origin", CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);

        // Written by a clone and by nothing else, so a repository built with remote add and
        // fetch has no such ref and answers nothing here.
        var result = await _git
            .RunAsync(
                root,
                GitCommand.Of("symbolic-ref", "--short", $"refs/remotes/{remote}/HEAD"),
                cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        var name = result.Output.Trim();
        var prefix = remote + "/";

        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }

    public async Task<bool> ExistsAsync(
        string root, string revision, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var result = await _git
            .RunAsync(
                root,
                GitCommand.Of("rev-parse", "--quiet", "--verify", revision + "^{commit}"),
                cancellation)
            .ConfigureAwait(false);

        return result.Succeeded;
    }

    public async Task<GitDivergence?> CompareAsync(
        string root, string baseline, string head, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(head);

        // The three dot form counts each side of the merge base, which is the pair of
        // numbers a person reads as ahead and behind.
        var result = await _git
            .RunAsync(
                root,
                GitCommand.Of("rev-list", "--left-right", "--count", $"{baseline}...{head}"),
                cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        var counts = result.Output.Split(
            [' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        if (counts.Length < 2
            || !int.TryParse(counts[0], CultureInfo.InvariantCulture, out var behind)
            || !int.TryParse(counts[1], CultureInfo.InvariantCulture, out var ahead))
        {
            return null;
        }

        // Left is what the baseline has alone, which is what the head is behind by.
        return new GitDivergence(ahead, behind);
    }

    public async Task<string?> ReadHeadBranchAsync(
        string root, CancellationToken cancellation = default)
    {
        var result = await _git
            .RunAsync(root, GitCommand.Of("symbolic-ref", "--short", "--quiet", "HEAD"), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded && result.Output.Trim() is { Length: > 0 } name ? name : null;
    }

    public async Task<string?> ReadMergeBaseAsync(
        string root, string first, string second, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(first);
        ArgumentException.ThrowIfNullOrWhiteSpace(second);

        // Two histories with no common commit are not an error to git either, it writes
        // nothing and exits non zero, which reads here as no answer.
        var result = await _git
            .RunAsync(root, GitCommand.Of("merge-base", first, second), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded && result.Output.Trim() is { Length: > 0 } hash ? hash : null;
    }
}
