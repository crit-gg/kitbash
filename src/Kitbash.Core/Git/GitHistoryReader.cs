using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>
/// Reads commits out of <c>git log</c>, one record per commit.
/// </summary>
public sealed class GitHistoryReader : IGitHistoryReader
{
    /// <summary>
    /// Twelve fields in this order, separated by the unit separator. The body is last so a
    /// message holding one of these bytes cannot push a later field out of place.
    /// </summary>
    private const string Format =
        "--format=%H%x1f%h%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%D%x1f%s%x1f%b";

    private const int Fields = 12;

    private readonly IGitRunner _git;

    public GitHistoryReader(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<IReadOnlyList<GitCommit>> ReadAsync(
        string root, GitHistoryQuery query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await _git.RunAsync(root, new GitCommand(Arguments(query)), cancellation)
            .ConfigureAwait(false);

        // A repository with no commits fails here rather than answering nothing, since there
        // is no head to walk from.
        return result.Succeeded ? Read(result.Records) : [];
    }

    public async Task<GitCommit?> ReadOneAsync(
        string root, string revision, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var found = await ReadAsync(
            root,
            new GitHistoryQuery(Limit: 1) { Revisions = [revision], FirstParentOnly = false },
            cancellation).ConfigureAwait(false);

        return found.Count > 0 ? found[0] : null;
    }

    private static List<string> Arguments(GitHistoryQuery query)
    {
        var arguments = new List<string>
        {
            "log",

            // Ends each commit with a null byte rather than a newline, which is the only
            // separator a commit message cannot hold.
            "-z",
            Format,
        };

        if (query.Limit > 0)
        {
            arguments.Add("--max-count=" + query.Limit.ToString(CultureInfo.InvariantCulture));
        }

        if (query.Skip > 0)
        {
            arguments.Add("--skip=" + query.Skip.ToString(CultureInfo.InvariantCulture));
        }

        if (query.FirstParentOnly)
        {
            arguments.Add("--first-parent");
        }

        if (query.EveryRef)
        {
            arguments.Add("--all");
        }

        arguments.AddRange(query.Revisions);

        // Everything after this is a path, so a branch and a file sharing a name cannot be
        // mistaken for each other.
        if (query.Path is { Length: > 0 } path)
        {
            arguments.Add("--");
            arguments.Add(path);
        }

        return arguments;
    }

    private static List<GitCommit> Read(IReadOnlyList<string> records)
    {
        var commits = new List<GitCommit>(records.Count);

        foreach (var record in records)
        {
            // Git separates commits with the null byte and still writes a newline between
            // one commit and the next, so the leading one belongs to neither.
            if (Read(record.TrimStart('\n', '\r')) is { } commit)
            {
                commits.Add(commit);
            }
        }

        return commits;
    }

    private static GitCommit? Read(string record)
    {
        var fields = record.Split('\x1f', Fields);

        if (fields.Length < Fields)
        {
            return null;
        }

        return new GitCommit(
            fields[0],
            fields[1],
            Split(fields[2], ' '),
            fields[3],
            fields[4],
            When(fields[5]),
            fields[6],
            fields[7],
            When(fields[8]),
            fields[10],
            fields[11].Trim('\n', '\r'),
            Split(fields[9], ", "));
    }

    /// <summary>Reads git's strict ISO 8601, which carries the offset the commit was made at.</summary>
    private static DateTimeOffset When(string value) =>
        DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : default;

    private static string[] Split(string value, string separator) =>
        value.Length == 0 ? [] : value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

    private static string[] Split(string value, char separator) =>
        value.Length == 0 ? [] : value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
}
