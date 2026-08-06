using Kitbash.Core.IO;

namespace Kitbash.Core.Git;

/// <summary>
/// Writes a merge driver into one repository, as config plus attributes. Both go under the
/// git directory rather than into the working tree, so nothing a person would commit moves.
/// </summary>
public sealed class GitMergeDrivers : IGitMergeDrivers
{
    /// <summary>Git reads this before any attributes file in the working tree.</summary>
    private const string Attributes = "attributes";

    private const string Unspecified = "unspecified";

    private readonly IGitRunner _git;
    private readonly IFileSystem _files;

    public GitMergeDrivers(IGitRunner git, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(files);

        _git = git;
        _files = files;
    }

    public async Task<GitMergeDriverOutcome> EnsureAsync(
        string root, GitMergeDriver driver, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(driver);

        var wrote = await ConfigureAsync(root, driver, cancellation).ConfigureAwait(false);

        if (wrote is null)
        {
            return GitMergeDriverOutcome.Failed;
        }

        var claimed = false;
        List<string> taking = [];

        foreach (var pattern in driver.Patterns)
        {
            var held = await HolderAsync(root, pattern, cancellation).ConfigureAwait(false);

            if (held is null)
            {
                return GitMergeDriverOutcome.Failed;
            }

            if (string.Equals(held, Unspecified, StringComparison.Ordinal))
            {
                taking.Add(pattern);
            }
            else if (!string.Equals(held, driver.Name, StringComparison.Ordinal))
            {
                claimed = true;
            }
        }

        if (taking.Count > 0 && !await ClaimAsync(root, driver, taking, cancellation).ConfigureAwait(false))
        {
            return GitMergeDriverOutcome.Failed;
        }

        if (claimed)
        {
            return GitMergeDriverOutcome.Claimed;
        }

        return wrote is true || taking.Count > 0 ? GitMergeDriverOutcome.Wired : GitMergeDriverOutcome.Ready;
    }

    /// <summary>True when the config was written, false when it already said this, null on failure.</summary>
    private async Task<bool?> ConfigureAsync(
        string root, GitMergeDriver driver, CancellationToken cancellation)
    {
        var key = "merge." + driver.Name + ".driver";

        var read = await _git
            .RunAsync(root, GitCommand.Of("config", "--local", "--get", key), cancellation)
            .ConfigureAwait(false);

        if (read.Outcome != GitRunOutcome.Ran)
        {
            return null;
        }

        // Exit one is the key being unset, which is the ordinary first time.
        if (read.Succeeded && string.Equals(read.Output.Trim(), driver.Command, StringComparison.Ordinal))
        {
            return false;
        }

        var title = await _git
            .RunAsync(
                root,
                GitCommand.Of("config", "--local", "merge." + driver.Name + ".name", driver.Title),
                cancellation)
            .ConfigureAwait(false);

        var command = await _git
            .RunAsync(root, GitCommand.Of("config", "--local", key, driver.Command), cancellation)
            .ConfigureAwait(false);

        if (!title.Succeeded || !command.Succeeded)
        {
            return null;
        }

        return true;
    }

    /// <summary>
    /// Which driver git already gives this pattern, <see cref="Unspecified"/> when none does,
    /// or null when it could not be asked. Asked of git so an attributes file anywhere counts.
    /// </summary>
    private async Task<string?> HolderAsync(string root, string pattern, CancellationToken cancellation)
    {
        var result = await _git
            .RunAsync(root, GitCommand.Of("check-attr", "-z", "merge", "--", Probe(pattern)), cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        // The null separated form is the path, the attribute and its value, in that order.
        var records = result.Records;

        return records.Count >= 3 ? records[2] : null;
    }

    private async Task<bool> ClaimAsync(
        string root, GitMergeDriver driver, IReadOnlyList<string> patterns, CancellationToken cancellation)
    {
        var common = await CommonAsync(root, cancellation).ConfigureAwait(false);

        if (common is null)
        {
            return false;
        }

        var directory = Path.Combine(common, "info");
        var file = Path.Combine(directory, Attributes);

        var text = _files.FileExists(file) ? _files.ReadAllText(file) : "";

        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            text += "\n";
        }

        foreach (var pattern in patterns)
        {
            text += pattern + " merge=" + driver.Name + "\n";
        }

        try
        {
            _files.CreateDirectory(directory);
            _files.WriteAllText(file, text);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Where the attributes git reads live. A linked worktree keeps its own git directory but
    /// shares this one, and git looks for the file here rather than beside the worktree.
    /// </summary>
    private async Task<string?> CommonAsync(string root, CancellationToken cancellation)
    {
        var result = await _git
            .RunAsync(root, GitCommand.Of("rev-parse", "--git-common-dir"), cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Output.Trim() is not { Length: > 0 } said)
        {
            return null;
        }

        // Answered relative to the directory git ran in, which is the root passed in here.
        return Path.GetFullPath(said, root);
    }

    /// <summary>A name the pattern matches, since git answers about a path rather than a pattern.</summary>
    private static string Probe(string pattern) => pattern.Replace('*', 'x').Replace('?', 'x');
}
