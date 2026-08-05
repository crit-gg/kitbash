using System.Globalization;

namespace Kitbash.Core.Git;

/// <summary>
/// Reads the null separated porcelain v2 form, which is the one git promises not to change
/// between versions and the only one safe for a path holding a space or a newline.
/// </summary>
public sealed class GitFileStatusReader : IGitFileStatusReader
{
    private readonly IGitRunner _git;

    public GitFileStatusReader(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<IReadOnlyList<GitFileStatus>> ReadAsync(
        string root, bool includeIgnored = false, CancellationToken cancellation = default)
    {
        var arguments = new List<string>
        {
            "status",
            "--porcelain=v2",
            "-z",

            // Every untracked file rather than the folder holding them, since a file list
            // shows files and a folder cannot be staged on its own.
            "--untracked-files=all",
        };

        if (includeIgnored)
        {
            arguments.Add("--ignored=matching");
        }

        var result = await _git.RunAsync(root, new GitCommand(arguments), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? Read(result.Records) : [];
    }

    private static List<GitFileStatus> Read(IReadOnlyList<string> records)
    {
        var files = new List<GitFileStatus>();

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];

            if (record.Length < 2)
            {
                continue;
            }

            switch (record[0])
            {
                case '1':
                    if (Changed(record) is { } changed)
                    {
                        files.Add(changed);
                    }

                    break;

                case '2':
                    // A rename writes the path it came from as its own record, right after.
                    var from = i + 1 < records.Count ? records[++i] : "";

                    if (Renamed(record, from) is { } renamed)
                    {
                        files.Add(renamed);
                    }

                    break;

                case 'u':
                    if (Field(record, 10) is { Length: > 0 } unmerged)
                    {
                        files.Add(new GitFileStatus(
                            unmerged, null, GitChangeKind.Unmerged, GitChangeKind.Unmerged));
                    }

                    break;

                case '?':
                    files.Add(new GitFileStatus(
                        record[2..], null, GitChangeKind.None, GitChangeKind.Untracked));
                    break;

                case '!':
                    files.Add(new GitFileStatus(
                        record[2..], null, GitChangeKind.None, GitChangeKind.Ignored));
                    break;

                default:
                    break;
            }
        }

        return files;
    }

    /// <summary>Reads <c>1 XY sub mH mI mW hH hI path</c>.</summary>
    private static GitFileStatus? Changed(string record)
    {
        var fields = record.Split(' ', 9);

        if (fields.Length < 9 || fields[1].Length < 2)
        {
            return null;
        }

        return new GitFileStatus(fields[8], null, Kind(fields[1][0]), Kind(fields[1][1]));
    }

    /// <summary>Reads <c>2 XY sub mH mI mW hH hI Xscore path</c> and the path it came from.</summary>
    private static GitFileStatus? Renamed(string record, string from)
    {
        var fields = record.Split(' ', 10);

        if (fields.Length < 10 || fields[1].Length < 2)
        {
            return null;
        }

        _ = int.TryParse(fields[8][1..], CultureInfo.InvariantCulture, out var similarity);

        return new GitFileStatus(
            fields[9],
            from.Length > 0 ? from : null,
            Kind(fields[1][0]),
            Kind(fields[1][1]),
            similarity);
    }

    /// <summary>The field at that position, where the last one is the rest of the record.</summary>
    private static string Field(string record, int index)
    {
        var fields = record.Split(' ', index + 1);

        return fields.Length > index ? fields[index] : "";
    }

    private static GitChangeKind Kind(char letter) => letter switch
    {
        'M' => GitChangeKind.Modified,
        'A' => GitChangeKind.Added,
        'D' => GitChangeKind.Deleted,
        'R' => GitChangeKind.Renamed,
        'C' => GitChangeKind.Copied,
        'T' => GitChangeKind.TypeChanged,
        'U' => GitChangeKind.Unmerged,
        _ => GitChangeKind.None,
    };
}
