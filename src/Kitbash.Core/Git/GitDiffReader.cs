using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>
/// Reads diffs by running git and parsing the unified form it writes.
/// </summary>
public sealed class GitDiffReader : IGitDiffReader
{
    private readonly IGitRunner _git;
    private readonly GitPatchReader _patches;

    public GitDiffReader(IGitRunner git, GitPatchReader patches)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(patches);

        _git = git;
        _patches = patches;
    }

    public async Task<IReadOnlyList<GitChange>> ReadChangesAsync(
        string root, GitDiffScope scope, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var arguments = Arguments(scope, GitDiffOptions.Default);
        arguments.Add("--name-status");

        // Fields separated by the null byte, which is the only separator a path cannot hold.
        arguments.Add("-z");

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? ReadNameStatus(result.Records) : [];
    }

    public async Task<IReadOnlyList<GitPatch>> ReadPatchesAsync(
        string root,
        GitDiffScope scope,
        IReadOnlyList<string>? paths = null,
        GitDiffOptions? options = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var arguments = Arguments(scope, options ?? GitDiffOptions.Default);
        arguments.Add("--patch");

        if (paths is { Count: > 0 })
        {
            arguments.Add("--");
            arguments.AddRange(paths);
        }

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? _patches.Read(result.Output) : [];
    }

    public async Task<GitPatch?> ReadPatchAsync(
        string root,
        GitDiffScope scope,
        string path,
        GitDiffOptions? options = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var found = await ReadPatchesAsync(root, scope, [path], options, cancellation)
            .ConfigureAwait(false);

        return found.Count > 0 ? found[0] : null;
    }

    public async Task<GitPatch?> ReadUntrackedPatchAsync(
        string root, string path, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var command = GitCommand.Of(
            "-c", "diff.noprefix=false",
            "-c", "diff.mnemonicPrefix=false",
            "diff",
            "--no-index",
            "--no-ext-diff",
            "--no-color",
            "--patch",
            "--",

            // Git matches this name itself rather than opening it, on Windows as well.
            "/dev/null",
            path);

        var result = await _git.RunAsync(root, command, cancellation).ConfigureAwait(false);

        // With --no-index git reports a difference through the exit code, the way diff does,
        // so one means it worked and found something.
        if (result.Outcome != GitRunOutcome.Ran || result.ExitCode > 1)
        {
            return null;
        }

        var patches = _patches.Read(result.Output);

        return patches.Count > 0
            ? patches[0] with { Path = path, Kind = GitChangeKind.Added }
            : null;
    }

    /// <summary>
    /// Reads the null separated name status form. Each entry is a letter and a path, except
    /// a rename or a copy, which carries a score and both paths.
    /// </summary>
    private static List<GitChange> ReadNameStatus(IReadOnlyList<string> records)
    {
        var changes = new List<GitChange>();

        for (var i = 0; i < records.Count; i++)
        {
            var status = records[i].Trim();

            if (status.Length == 0 || ++i >= records.Count)
            {
                break;
            }

            var kind = Kind(status[0]);

            if (kind is GitChangeKind.Renamed or GitChangeKind.Copied)
            {
                if (i + 1 >= records.Count)
                {
                    break;
                }

                _ = int.TryParse(
                    status[1..], CultureInfo.InvariantCulture, out var similarity);

                changes.Add(new GitChange(kind, records[i + 1], records[i], similarity));
                i++;
                continue;
            }

            changes.Add(new GitChange(kind, records[i]));
        }

        return changes;
    }

    private static GitChangeKind Kind(char letter) => letter switch
    {
        'A' => GitChangeKind.Added,
        'D' => GitChangeKind.Deleted,
        'R' => GitChangeKind.Renamed,
        'C' => GitChangeKind.Copied,
        'T' => GitChangeKind.TypeChanged,
        'U' => GitChangeKind.Unmerged,
        _ => GitChangeKind.Modified,
    };

    private static List<string> Arguments(GitDiffScope scope, GitDiffOptions options)
    {
        var arguments = new List<string>
        {
            // A repository can turn the prefixes off or make them mean the side rather than
            // the version, and the patch reader takes them off by position.
            "-c", "diff.noprefix=false",
            "-c", "diff.mnemonicPrefix=false",
        };

        arguments.Add(scope.Side == GitDiffSide.Commit ? "diff-tree" : "diff");

        // A configured external tool would answer instead of git, and a textconv filter
        // would give text that cannot be applied back.
        arguments.Add("--no-ext-diff");
        arguments.Add("--no-textconv");
        arguments.Add("--no-color");

        // Full blob names rather than abbreviated ones, so git apply can be sure which
        // version a patch built from this is patching.
        arguments.Add("--full-index");
        arguments.Add("--unified=" + options.Context.ToString(CultureInfo.InvariantCulture));

        if (options.DetectRenames)
        {
            arguments.Add("--find-renames");
        }
        else
        {
            arguments.Add("--no-renames");
        }

        if (options.IgnoreWhitespace)
        {
            arguments.Add("--ignore-all-space");
        }

        switch (scope.Side)
        {
            case GitDiffSide.Staged:
                arguments.Add("--cached");
                break;

            case GitDiffSide.Uncommitted:
                arguments.Add("HEAD");
                break;

            case GitDiffSide.Range:
                arguments.Add(scope.From ?? "HEAD");
                arguments.Add(scope.To ?? "HEAD");
                break;

            case GitDiffSide.Commit:
                // diff-tree rather than diff, so a commit with no parent still has something
                // to be compared with, and a merge reads as its change against the branch it
                // was merged into.
                arguments.Add("-r");
                arguments.Add("--root");
                arguments.Add("--no-commit-id");

                // A merge otherwise writes nothing at all, and the plain --first-parent does
                // not narrow it: measured on git 2.55, that one writes both parents.
                arguments.Add("--diff-merges=first-parent");
                arguments.Add(scope.From ?? "HEAD");
                break;

            case GitDiffSide.Unstaged:
            default:
                break;
        }

        return arguments;
    }
}
