using System.Globalization;
using System.Text;

namespace Kitbash.Core.Git;

/// <summary>
/// Writes a patch holding some of a file's hunks, for <c>git apply</c> to put in the index.
/// </summary>
public sealed class GitPatchWriter
{
    /// <summary>
    /// Builds a patch from the hunks whose index is in <paramref name="hunks"/>.
    /// </summary>
    /// <param name="reverse">
    /// True when the patch will be applied with <c>-R</c>, which is how a hunk is taken back
    /// out of the index. It changes which side of each header moves.
    /// </param>
    /// <returns>Empty when nothing was selected, which is not a patch git will accept.</returns>
    public string Write(GitPatch patch, IReadOnlyCollection<int> hunks, bool reverse = false)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(hunks);

        if (hunks.Count == 0 || patch.IsBinary)
        {
            return "";
        }

        var text = new StringBuilder();

        foreach (var line in patch.Header)
        {
            text.Append(line).Append('\n');
        }

        // A hunk left out changes how long the file is, so every later hunk starts somewhere
        // else. The side that moves is the one describing the file git will apply this to,
        // which is the old side going forward and the new side coming back.
        var drift = 0;

        for (var i = 0; i < patch.Hunks.Count; i++)
        {
            var hunk = patch.Hunks[i];

            if (!hunks.Contains(i))
            {
                drift += reverse
                    ? hunk.NewCount - hunk.OldCount
                    : hunk.OldCount - hunk.NewCount;
                continue;
            }

            Write(text, hunk, drift, reverse);
        }

        return text.ToString();
    }

    /// <summary>The whole patch, unchanged, which is every hunk selected.</summary>
    public string Write(GitPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return Write(patch, [.. Enumerable.Range(0, patch.Hunks.Count)]);
    }

    private static void Write(StringBuilder text, GitHunk hunk, int drift, bool reverse)
    {
        var oldStart = reverse ? hunk.OldStart + drift : hunk.OldStart;
        var newStart = reverse ? hunk.NewStart : hunk.NewStart + drift;

        text.Append("@@ -")
            .Append(Range(oldStart, hunk.OldCount))
            .Append(" +")
            .Append(Range(newStart, hunk.NewCount))
            .Append(" @@");

        if (hunk.Heading.Length > 0)
        {
            text.Append(' ').Append(hunk.Heading);
        }

        text.Append('\n');

        foreach (var line in hunk.Lines)
        {
            text.Append(Marker(line.Kind)).Append(line.Text).Append('\n');
        }
    }

    /// <summary>
    /// Always with its count, which git allows everywhere even where its own output leaves a
    /// count of one out. The numbers are the ones read back, so nothing is worked out here.
    /// </summary>
    private static string Range(int start, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{start},{count}");

    private static string Marker(GitDiffLineKind kind) => kind switch
    {
        GitDiffLineKind.Added => "+",
        GitDiffLineKind.Removed => "-",

        // The space is part of what git writes, and a patch is compared with git's own.
        GitDiffLineKind.NoNewline => "\\ ",
        _ => " ",
    };
}
