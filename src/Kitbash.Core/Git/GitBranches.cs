using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>Reads and changes branches by running git.</summary>
public sealed class GitBranches : IGitBranches
{
    /// <summary>
    /// Nine fields separated by the unit separator. A ref name cannot hold a newline, so one
    /// line per branch is safe here in a way it would not be for a path.
    /// </summary>
    private const string Format =
        "--format=%(refname)%1f%(refname:short)%1f%(objectname)%1f%(upstream:short)%1f" +
        "%(upstream:track)%1f%(HEAD)%1f%(committerdate:iso-strict)%1f%(authorname)" +
        "%1f%(contents:subject)";

    private const int Fields = 9;

    private readonly IGitRunner _git;

    public GitBranches(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<IReadOnlyList<GitBranch>> ReadAsync(
        string root, bool includeRemote = true, CancellationToken cancellation = default)
    {
        var arguments = new List<string>
        {
            "for-each-ref",
            Format,
            "--sort=-committerdate",
            "refs/heads",
        };

        if (includeRemote)
        {
            arguments.Add("refs/remotes");
        }

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return [];
        }

        var branches = new List<GitBranch>();

        foreach (var line in result.Lines)
        {
            if (Read(line) is { } branch)
            {
                branches.Add(branch);
            }
        }

        return branches;
    }

    public async Task<IReadOnlyList<string>> ReadMergedAsync(
        string root,
        string into,
        bool includeRemote = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(into);

        var arguments = new List<string>
        {
            "for-each-ref",
            "--format=%(refname)",
            "--merged",
            into,
            "refs/heads",
        };

        if (includeRemote)
        {
            arguments.Add("refs/remotes");
        }

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? result.Lines : [];
    }

    public Task<GitResult> CreateAsync(
        string root,
        string name,
        string? startPoint = null,
        bool switchTo = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var arguments = switchTo
            ? new List<string> { "switch", "--create", name }
            : ["branch", name];

        if (startPoint is { Length: > 0 } start)
        {
            arguments.Add(start);
        }

        return _git.RunAsync(root, new GitCommand(arguments), cancellation);
    }

    public Task<GitResult> SwitchAsync(
        string root, string name, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _git.RunAsync(root, GitCommand.Of("switch", name), cancellation);
    }

    public Task<GitResult> SwitchToCommitAsync(
        string root, string revision, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        return _git.RunAsync(root, GitCommand.Of("switch", "--detach", revision), cancellation);
    }

    public Task<GitResult> DeleteAsync(
        string root, string name, bool force = false, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var arguments = new List<string> { "branch", "--delete" };

        if (force)
        {
            arguments.Add("--force");
        }

        arguments.Add(name);

        return _git.RunAsync(root, new GitCommand(arguments), cancellation);
    }

    public Task<GitResult> RenameAsync(
        string root, string from, string to, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        return _git.RunAsync(root, GitCommand.Of("branch", "--move", from, to), cancellation);
    }

    private static GitBranch? Read(string line)
    {
        var fields = line.Split('\x1f', Fields);

        if (fields.Length < Fields)
        {
            return null;
        }

        // refs/remotes/<remote>/HEAD is the remote's default branch pointer, and git shortens
        // it to the remote's own name, so a caller listing branches gets a row called origin.
        if (fields[0].StartsWith("refs/remotes/", StringComparison.Ordinal)
            && fields[0].EndsWith("/HEAD", StringComparison.Ordinal))
        {
            return null;
        }

        Track(fields[4], out var ahead, out var behind, out var gone);

        return new GitBranch(
            fields[1],
            fields[0],
            fields[0].StartsWith("refs/remotes/", StringComparison.Ordinal),

            // Git marks the current branch with an asterisk and every other with a space.
            fields[5].Trim() == "*",
            fields[2],
            fields[3].Length > 0 ? fields[3] : null,
            gone,
            ahead,
            behind,
            When(fields[6]),
            fields[7],
            fields[8]);
    }

    /// <summary>
    /// Reads git's own tracking note, which is <c>[ahead 1, behind 2]</c>, or <c>[gone]</c>
    /// when the upstream was deleted, or empty when there is none or it is level.
    /// </summary>
    private static void Track(string value, out int ahead, out int behind, out bool gone)
    {
        ahead = 0;
        behind = 0;
        gone = value.Contains("gone", StringComparison.Ordinal);

        foreach (var part in value.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("ahead ", StringComparison.Ordinal))
            {
                _ = int.TryParse(part[6..], CultureInfo.InvariantCulture, out ahead);
            }
            else if (part.StartsWith("behind ", StringComparison.Ordinal))
            {
                _ = int.TryParse(part[7..], CultureInfo.InvariantCulture, out behind);
            }
        }
    }

    private static DateTimeOffset? When(string value) =>
        DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : null;
}
