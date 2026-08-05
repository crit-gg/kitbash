using System.Globalization;
using System.Text;

namespace Kitbash.Core.Git;

/// <summary>
/// Reads git's unified diff into files, hunks and lines. Nothing here runs git, so a patch
/// from anywhere can be read.
/// </summary>
public sealed class GitPatchReader
{
    private const string FileMarker = "diff --git ";

    /// <summary>
    /// Reads a patch that may cover many files. Anything before the first file marker is
    /// ignored, so output that begins with a commit header can be handed over whole.
    /// </summary>
    public IReadOnlyList<GitPatch> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Split on the newline alone. A carriage return before it belongs to the file's own
        // content, and dropping it would change what a hunk says the line is.
        var lines = text.Split('\n');
        var patches = new List<GitPatch>();

        var at = 0;

        while (at < lines.Length)
        {
            if (!lines[at].StartsWith(FileMarker, StringComparison.Ordinal))
            {
                at++;
                continue;
            }

            patches.Add(ReadFile(lines, ref at));
        }

        return patches;
    }

    /// <summary>Reads one file's worth, leaving the index on the line after it.</summary>
    private static GitPatch ReadFile(string[] lines, ref int at)
    {
        var header = new List<string> { lines[at] };
        var hunks = new List<GitHunk>();

        string? oldPath = null;
        string? newPath = null;
        var kind = GitChangeKind.Modified;
        var binary = false;

        at++;

        // The header runs until the first hunk or the next file, whichever comes first.
        while (at < lines.Length && !IsHunkStart(lines[at]) && !lines[at].StartsWith(FileMarker, StringComparison.Ordinal))
        {
            var line = lines[at];
            header.Add(line);

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Added;
            }
            else if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Deleted;
            }
            else if (line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Renamed;
                oldPath = Unquote(line["rename from ".Length..]);
            }
            else if (line.StartsWith("rename to ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Renamed;
                newPath = Unquote(line["rename to ".Length..]);
            }
            else if (line.StartsWith("copy from ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Copied;
                oldPath = Unquote(line["copy from ".Length..]);
            }
            else if (line.StartsWith("copy to ", StringComparison.Ordinal))
            {
                kind = GitChangeKind.Copied;
                newPath = Unquote(line["copy to ".Length..]);
            }
            else if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                oldPath = Side(line[4..]) ?? oldPath;
            }
            else if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                newPath = Side(line[4..]) ?? newPath;
            }
            else if (line.StartsWith("GIT binary patch", StringComparison.Ordinal)
                || (line.StartsWith("Binary files ", StringComparison.Ordinal) && line.EndsWith(" differ", StringComparison.Ordinal)))
            {
                binary = true;
            }

            at++;
        }

        while (at < lines.Length && IsHunkStart(lines[at]))
        {
            hunks.Add(ReadHunk(lines, ref at));
        }

        // A rename with no content change carries no --- or +++ pair at all, so the paths
        // come from whichever of the two forms this file used.
        var path = newPath ?? oldPath ?? PathFromMarker(header[0]);

        return new GitPatch(
            path,
            kind is GitChangeKind.Renamed or GitChangeKind.Copied ? oldPath : null,
            kind,
            binary,
            header,
            hunks);
    }

    private static GitHunk ReadHunk(string[] lines, ref int at)
    {
        Header(lines[at], out var oldStart, out var oldCount, out var newStart, out var newCount, out var heading);
        at++;

        var body = new List<GitDiffLine>();
        var oldLine = oldStart;
        var newLine = newStart;

        // The counts in the header say how long the hunk is. Reading to them rather than to
        // the next marker is what keeps an empty line apart from the end of the output.
        while (at < lines.Length && (oldLine - oldStart < oldCount || newLine - newStart < newCount))
        {
            var line = lines[at];

            if (line.Length == 0)
            {
                // Git writes an empty context line as a lone space. A truly empty line here
                // is a patch from some other tool, and means the same thing.
                body.Add(new GitDiffLine(GitDiffLineKind.Context, "", oldLine++, newLine++));
                at++;
                continue;
            }

            if (IsHunkStart(line) || line.StartsWith(FileMarker, StringComparison.Ordinal))
            {
                break;
            }

            switch (line[0])
            {
                case ' ':
                    body.Add(new GitDiffLine(GitDiffLineKind.Context, line[1..], oldLine++, newLine++));
                    break;
                case '+':
                    body.Add(new GitDiffLine(GitDiffLineKind.Added, line[1..], null, newLine++));
                    break;
                case '-':
                    body.Add(new GitDiffLine(GitDiffLineKind.Removed, line[1..], oldLine++, null));
                    break;
                case '\\':
                    body.Add(new GitDiffLine(GitDiffLineKind.NoNewline, line[1..].TrimStart()));
                    break;
                default:
                    return new GitHunk(oldStart, oldCount, newStart, newCount, heading, body);
            }

            at++;
        }

        // The note about a missing final newline sits after the last counted line.
        while (at < lines.Length && lines[at].StartsWith('\\'))
        {
            body.Add(new GitDiffLine(GitDiffLineKind.NoNewline, lines[at][1..].TrimStart()));
            at++;
        }

        return new GitHunk(oldStart, oldCount, newStart, newCount, heading, body);
    }

    private static bool IsHunkStart(string line) =>
        line.StartsWith("@@ ", StringComparison.Ordinal);

    /// <summary>Reads <c>@@ -1,3 +1,4 @@ heading</c>, where either count may be left out.</summary>
    private static void Header(
        string line,
        out int oldStart,
        out int oldCount,
        out int newStart,
        out int newCount,
        out string heading)
    {
        oldStart = 0;
        oldCount = 0;
        newStart = 0;
        newCount = 0;
        heading = "";

        var close = line.IndexOf(" @@", 3, StringComparison.Ordinal);

        if (close < 0)
        {
            return;
        }

        heading = line[(close + 3)..].TrimStart();

        foreach (var part in line[3..close].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 2)
            {
                continue;
            }

            Range(part[1..], out var start, out var count);

            if (part[0] == '-')
            {
                oldStart = start;
                oldCount = count;
            }
            else if (part[0] == '+')
            {
                newStart = start;
                newCount = count;
            }
        }
    }

    /// <summary>Reads <c>12,3</c> or <c>12</c>, where a missing count means one line.</summary>
    private static void Range(string value, out int start, out int count)
    {
        var comma = value.IndexOf(',', StringComparison.Ordinal);

        if (comma < 0)
        {
            _ = int.TryParse(value, CultureInfo.InvariantCulture, out start);
            count = 1;
            return;
        }

        _ = int.TryParse(value[..comma], CultureInfo.InvariantCulture, out start);
        _ = int.TryParse(value[(comma + 1)..], CultureInfo.InvariantCulture, out count);
    }

    /// <summary>
    /// Reads the path off a <c>---</c> or <c>+++</c> line, dropping the one letter prefix.
    /// Null for <c>/dev/null</c>, which is how git writes the missing side.
    /// </summary>
    private static string? Side(string value)
    {
        var path = value;

        // Git appends a tab and a timestamp for a patch meant to be read by others. Its own
        // output has none, but a patch handed in from elsewhere may.
        var tab = path.IndexOf('\t', StringComparison.Ordinal);

        if (tab >= 0)
        {
            path = path[..tab];
        }

        path = Unquote(path);

        if (path == "/dev/null")
        {
            return null;
        }

        return path.Length > 2 && path[1] == '/' ? path[2..] : path;
    }

    /// <summary>
    /// Last resort when a file has neither a rename pair nor a <c>+++</c> line, which is a
    /// mode change on its own. Both halves are the same path there, so the ambiguity a space
    /// would cause does not arise.
    /// </summary>
    private static string PathFromMarker(string marker)
    {
        var rest = marker[FileMarker.Length..];
        var half = rest.Length / 2;

        return half > 2 ? Unquote(rest[..half].TrimEnd())[2..] : rest;
    }

    /// <summary>
    /// Undoes git's C style quoting, which it applies to a path holding a quote, a backslash
    /// or a control character even with <c>core.quotepath</c> off.
    /// </summary>
    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var body = value[1..^1];
        var text = new StringBuilder(body.Length);

        // Octal escapes are one byte each, so a character outside ASCII arrives as several
        // and has to be decoded as UTF 8 rather than one escape at a time.
        var bytes = new List<byte>();

        void Flush()
        {
            if (bytes.Count > 0)
            {
                text.Append(Encoding.UTF8.GetString([.. bytes]));
                bytes.Clear();
            }
        }

        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '\\' || i + 1 >= body.Length)
            {
                Flush();
                text.Append(body[i]);
                continue;
            }

            var next = body[++i];

            switch (next)
            {
                case 'a': Flush(); text.Append('\a'); break;
                case 'b': Flush(); text.Append('\b'); break;
                case 'f': Flush(); text.Append('\f'); break;
                case 'n': Flush(); text.Append('\n'); break;
                case 'r': Flush(); text.Append('\r'); break;
                case 't': Flush(); text.Append('\t'); break;
                case 'v': Flush(); text.Append('\v'); break;
                case '"': Flush(); text.Append('"'); break;
                case '\\': Flush(); text.Append('\\'); break;
                default:
                    if (next is >= '0' and <= '7' && i + 2 < body.Length)
                    {
                        bytes.Add(Convert.ToByte(body.Substring(i, 3), 8));
                        i += 2;
                    }
                    else
                    {
                        Flush();
                        text.Append(next);
                    }

                    break;
            }
        }

        Flush();

        return text.ToString();
    }
}
