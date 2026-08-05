using System.Globalization;
using System.Text;

namespace Kitbash.Core.Git;

/// <summary>
/// Writes a patch holding some of a file's changes, for <c>git apply</c> to put in the index.
/// A whole hunk or a run of lines inside one.
/// </summary>
public sealed class GitPatchWriter
{
    /// <summary>
    /// Builds a patch from the lines in <paramref name="lines"/>. A context line is never
    /// picked and is always carried, so only added and removed lines are read from the set.
    /// </summary>
    /// <param name="reverse">
    /// True when the patch will be applied with <c>-R</c>, which is how a change is taken back
    /// out of the index. It changes which side of each header moves and which unpicked lines
    /// survive as context.
    /// </param>
    /// <returns>Empty when nothing was picked, which is not a patch git will accept.</returns>
    public string Write(GitPatch patch, IReadOnlyCollection<GitPatchLine> lines, bool reverse = false)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0 || patch.IsBinary)
        {
            return "";
        }

        // Read once per line of every hunk, so a caller passing a list does not turn this
        // into a scan of the selection each time.
        var picked = lines as IReadOnlySet<GitPatchLine> ?? new HashSet<GitPatchLine>(lines);

        var text = new StringBuilder();

        foreach (var line in patch.Header)
        {
            text.Append(line).Append('\n');
        }

        // A change left out means the file ends up a different length than the whole diff
        // would make it, so every later hunk starts somewhere else. The side that moves is
        // the one describing the file git will apply this to, which is the new side going
        // forward and the old side coming back.
        var drift = 0;
        var wrote = false;

        for (var index = 0; index < patch.Hunks.Count; index++)
        {
            var hunk = patch.Hunks[index];
            var built = Build(hunk, index, picked, reverse);

            if (built.Changes > 0)
            {
                Write(text, hunk, built, drift, reverse);
                wrote = true;
            }

            // How far this hunk moved the file against what the whole diff would have moved
            // it. A hunk taken whole contributes nothing and one left out contributes all of
            // it, and both fall out of the same sum.
            var moved = built.New - built.Old - (hunk.NewCount - hunk.OldCount);

            drift += reverse ? -moved : moved;
        }

        return wrote ? text.ToString() : "";
    }

    /// <summary>
    /// Builds a patch from the hunks whose index is in <paramref name="hunks"/>. Every changed
    /// line of those hunks is picked and no other.
    /// </summary>
    /// <inheritdoc cref="Write(GitPatch, IReadOnlyCollection{GitPatchLine}, bool)" path="/param|/returns"/>
    public string Write(GitPatch patch, IReadOnlyCollection<int> hunks, bool reverse = false)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(hunks);

        return Write(patch, Whole(patch, hunks), reverse);
    }

    /// <summary>The whole patch, unchanged, which is every hunk picked.</summary>
    public string Write(GitPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return Write(patch, (IReadOnlyCollection<int>)[.. Enumerable.Range(0, patch.Hunks.Count)]);
    }

    /// <summary>Every changed line of the named hunks.</summary>
    private static HashSet<GitPatchLine> Whole(GitPatch patch, IReadOnlyCollection<int> hunks)
    {
        HashSet<GitPatchLine> lines = [];

        foreach (var hunk in hunks)
        {
            if (hunk < 0 || hunk >= patch.Hunks.Count)
            {
                continue;
            }

            var body = patch.Hunks[hunk].Lines;

            for (var line = 0; line < body.Count; line++)
            {
                if (body[line].Kind is GitDiffLineKind.Added or GitDiffLineKind.Removed)
                {
                    lines.Add(new GitPatchLine(hunk, line));
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// One hunk rewritten to hold only the picked changes. <paramref name="Old"/> and
    /// <paramref name="New"/> are how many lines the result covers on each side, and
    /// <paramref name="Changes"/> is zero when the hunk would do nothing.
    /// </summary>
    private sealed record Built(string Body, int Old, int New, int Changes);

    /// <summary>
    /// Rewrites a hunk to hold only what was picked. An unpicked change on the side git is
    /// applying to has to stay as context, since that line really is in the file. An unpicked
    /// change on the other side is dropped, since it is not there to describe.
    /// </summary>
    private static Built Build(
        GitHunk hunk, int index, IReadOnlySet<GitPatchLine> lines, bool reverse)
    {
        var body = new StringBuilder();

        int old = 0, now = 0, changes = 0;
        var kept = false;

        for (var at = 0; at < hunk.Lines.Count; at++)
        {
            var line = hunk.Lines[at];

            // The note belongs to the line above it, so it goes wherever that went.
            if (line.Kind == GitDiffLineKind.NoNewline)
            {
                if (kept)
                {
                    body.Append("\\ ").Append(line.Text).Append('\n');
                }

                continue;
            }

            if (line.Kind == GitDiffLineKind.Context)
            {
                body.Append(' ').Append(line.Text).Append('\n');
                old++;
                now++;
                kept = true;
                continue;
            }

            var picked = lines.Contains(new GitPatchLine(index, at));
            var added = line.Kind == GitDiffLineKind.Added;

            if (picked)
            {
                body.Append(added ? '+' : '-').Append(line.Text).Append('\n');

                if (added)
                {
                    now++;
                }
                else
                {
                    old++;
                }

                changes++;
                kept = true;
                continue;
            }

            // Going forward git holds the old side, so an unpicked removal is still there and
            // an unpicked addition is not. Coming back it holds the new side, so it is the
            // other way round.
            if (added == reverse)
            {
                body.Append(' ').Append(line.Text).Append('\n');
                old++;
                now++;
                kept = true;
                continue;
            }

            kept = false;
        }

        return new Built(body.ToString(), old, now, changes);
    }

    private static void Write(StringBuilder text, GitHunk hunk, Built built, int drift, bool reverse)
    {
        var oldStart = reverse ? hunk.OldStart + drift : hunk.OldStart;
        var newStart = reverse ? hunk.NewStart : hunk.NewStart + drift;

        // A side the picking emptied names the line the rest goes in after, rather than a
        // line it no longer covers. A side that was already empty, as on a new file, is
        // git's own 0 and is left alone.
        text.Append("@@ -")
            .Append(Range(Start(oldStart, built.Old, hunk.OldCount), built.Old))
            .Append(" +")
            .Append(Range(Start(newStart, built.New, hunk.NewCount), built.New))
            .Append(" @@");

        if (hunk.Heading.Length > 0)
        {
            text.Append(' ').Append(hunk.Heading);
        }

        text.Append('\n').Append(built.Body);
    }

    private static int Start(int start, int count, int was) =>
        count == 0 && was > 0 ? start - 1 : start;

    /// <summary>
    /// Always with its count, which git allows everywhere even where its own output leaves a
    /// count of one out.
    /// </summary>
    private static string Range(int start, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{start},{count}");
}
