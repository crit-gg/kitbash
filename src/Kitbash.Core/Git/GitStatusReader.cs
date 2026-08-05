using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Git;

/// <summary>
/// Reads a repository by running git and parsing what it says.
/// </summary>
public sealed class GitStatusReader : IGitStatusReader
{
    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly IFileSystem _fileSystem;

    private string? _resolvedRoot;
    private GitPlaces? _resolvedPlaces;

    public GitStatusReader(IProcessRunner processes, IExternalTools tools, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _processes = processes;
        _tools = tools;
        _fileSystem = fileSystem;
    }

    public bool IsAvailable => Git is not null;

    // Whichever git a person pointed at, and the first on PATH otherwise. Resolved once
    // by IExternalTools, since the alternative is searching PATH on every refresh.
    private string? Git => _tools.Git.Path;

    public async Task<GitStatus?> ReadAsync(string root, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (Git is not { } git || !_fileSystem.DirectoryExists(root))
        {
            return null;
        }

        ProcessOutput output;

        try
        {
            output = await _processes.ReadAsync(
                ProcessRequest.CommandIn(root, git, "--no-optional-locks", "status", "--porcelain=v2", "--branch", "--untracked-files=no"),
                cancellation).ConfigureAwait(false);
        }
        catch (ProcessStartException)
        {
            // git was on PATH a moment ago and is not now, or cannot run here.
            return null;
        }

        // A folder that is not a repository is the ordinary case, and git says so with a
        // non zero exit rather than by failing to start.
        if (!output.Succeeded)
        {
            _resolvedRoot = null;
            _resolvedPlaces = null;
            return null;
        }

        var places = await PlacesAsync(git, root, cancellation).ConfigureAwait(false);

        return places is null ? null : Parse(output.Lines, places);
    }

    /// <summary>
    /// Asks git where it keeps this repository.
    /// </summary>
    private async Task<GitPlaces?> PlacesAsync(string git, string root, CancellationToken cancellation)
    {
        if (_resolvedPlaces is { } cached && string.Equals(_resolvedRoot, root, StringComparison.Ordinal))
        {
            return cached;
        }

        ProcessOutput output;

        try
        {
            output = await _processes.ReadAsync(
                ProcessRequest.CommandIn(root, git, "rev-parse", "--absolute-git-dir", "--git-common-dir", "--show-toplevel"),
                cancellation).ConfigureAwait(false);
        }
        catch (ProcessStartException)
        {
            return null;
        }

        if (!output.Succeeded || output.Lines.Count == 0 || output.Lines[0].Trim().Length == 0)
        {
            return null;
        }

        var directory = output.Lines[0].Trim();

        // Only --absolute-git-dir promises an absolute path. --git-common-dir answers
        // relative to the working directory git ran in, which is the root passed in here.
        var common = output.Lines.Count > 1 && output.Lines[1].Trim().Length > 0
            ? Path.GetFullPath(output.Lines[1].Trim(), root)
            : directory;

        var reftable = Path.Combine(common, "reftable");

        var places = new GitPlaces(
            directory,
            common,
            _fileSystem.DirectoryExists(reftable) ? reftable : null,
            output.Lines.Count > 2 ? output.Lines[2].Trim() : "");

        _resolvedRoot = root;
        _resolvedPlaces = places;

        return places;
    }

    private GitStatus Parse(IReadOnlyList<string> lines, GitPlaces places)
    {
        var branch = "";
        var commit = "";
        var detached = false;
        var hasUpstream = false;
        var ahead = 0;
        var behind = 0;
        var modified = 0;
        var staged = 0;
        var conflicted = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = line["# branch.head ".Length..].Trim();
                detached = branch == "(detached)";
            }
            else if (line.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                // Kept whether or not this head turns out to be detached, since git writes
                // this line before the one that says which it is.
                commit = line["# branch.oid ".Length..].Trim();
            }
            else if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                hasUpstream = true;
            }
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                Divergence(line["# branch.ab ".Length..], out ahead, out behind);
            }
            else if (line.StartsWith("u ", StringComparison.Ordinal))
            {
                conflicted++;
            }
            else if (line.StartsWith("1 ", StringComparison.Ordinal) || line.StartsWith("2 ", StringComparison.Ordinal))
            {
                // The two status letters are the index and the working tree, in that
                // order, and a dot means unchanged. A file can be both, and is counted in
                // both, which is what git itself reports.
                var state = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

                if (state.Length < 2 || state[1].Length < 2)
                {
                    continue;
                }

                if (state[1][0] != '.') { staged++; }
                if (state[1][1] != '.') { modified++; }
            }
        }

        // A detached head has no name, so it is shown as the short commit the way git itself
        // shows it. Settled here because the oid line arrives before the head line.
        if (detached)
        {
            branch = commit.Length > 0 ? Short(commit) : "detached";
        }

        return new GitStatus(
            places,
            branch,
            detached,
            hasUpstream,
            ahead,
            behind,
            modified,
            staged,
            conflicted,
            _fileSystem.GetLastWriteTime(Path.Combine(places.GitDirectory, "FETCH_HEAD")));
    }

    /// <summary>Reads <c>+2 -1</c> into two counts.</summary>
    private static void Divergence(string value, out int ahead, out int behind)
    {
        ahead = 0;
        behind = 0;

        foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 2 || !int.TryParse(part[1..], out var count))
            {
                continue;
            }

            if (part[0] == '+') { ahead = count; }
            else if (part[0] == '-') { behind = count; }
        }
    }

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;
}
