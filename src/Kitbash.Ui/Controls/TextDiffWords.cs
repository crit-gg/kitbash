namespace Kitbash.Ui.Controls;

/// <summary>
/// Works out which words inside a changed line actually differ, so a one character change in
/// a long line is visible without reading the whole line twice.
/// </summary>
public sealed class TextDiffWords
{
    /// <summary>
    /// Past this many words either side the pairing is dropped rather than compared. The
    /// comparison is quadratic, and a line that long is not read word by word anyway.
    /// </summary>
    private const int Most = 400;

    /// <summary>
    /// Where nearly the whole line differs, marking it says nothing the line kind did not.
    /// Measured as the share of the line the runs cover.
    /// </summary>
    private const double TooMuch = 0.75;

    /// <summary>
    /// The runs that differ between two lines, one list for each. Both are empty when the
    /// lines are the same, when either is empty, or when so much differs that marking it
    /// would only repeat what the kind already says.
    /// </summary>
    public (IReadOnlyList<TextDiffSpan> Old, IReadOnlyList<TextDiffSpan> New) Compare(
        string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        if (oldText.Length == 0 || newText.Length == 0 || string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return ([], []);
        }

        var before = Words(oldText);
        var after = Words(newText);

        // The ends usually agree, and trimming them is what keeps the comparison small.
        var head = SharedHead(before, after, oldText, newText);
        var tail = SharedTail(before, after, oldText, newText, head);

        var oldMiddle = before.Count - head - tail;
        var newMiddle = after.Count - head - tail;

        if (oldMiddle <= 0 && newMiddle <= 0)
        {
            return ([], []);
        }

        if (oldMiddle > Most || newMiddle > Most)
        {
            return ([], []);
        }

        var (oldChanged, newChanged) = Middle(before, after, head, oldMiddle, newMiddle, oldText, newText);

        // Marking nearly everything says nothing the kind did not already say.
        return Covers(oldChanged, oldText) > TooMuch || Covers(newChanged, newText) > TooMuch
            ? ([], [])
            : (oldChanged, newChanged);
    }

    /// <summary>
    /// The runs for one line against the line it is paired with, for a caller that only
    /// draws one side.
    /// </summary>
    public IReadOnlyList<TextDiffSpan> Against(string text, string other) =>
        Compare(other, text).New;

    /// <summary>
    /// Pairs the removed lines of each run with the added ones and marks the words that
    /// differ, returning the same lines with their runs filled in. A caller that already has
    /// runs, or wants none, does not have to call this.
    /// </summary>
    public IReadOnlyList<TextDiffLine> Mark(IReadOnlyList<TextDiffLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var marked = new TextDiffLine[lines.Count];

        for (var at = 0; at < lines.Count; at++)
        {
            marked[at] = lines[at];
        }

        var index = 0;

        while (index < marked.Length)
        {
            var removed = Run(marked, index, TextDiffLineKind.Removed);

            if (removed == index)
            {
                index++;
                continue;
            }

            var added = Run(marked, removed, TextDiffLineKind.Added);

            // A run of removals followed by a run of additions is one replacement, and the
            // lines are paired off in the order they were written.
            var pairs = Math.Min(removed - index, added - removed);

            for (var pair = 0; pair < pairs; pair++)
            {
                var before = marked[index + pair];
                var after = marked[removed + pair];

                var (old, now) = Compare(before.Text, after.Text);

                marked[index + pair] = before with { Changed = old };
                marked[removed + pair] = after with { Changed = now };
            }

            index = added > removed ? added : removed;
        }

        return marked;
    }

    /// <summary>Where the run of one kind starting at this index ends.</summary>
    private static int Run(TextDiffLine[] lines, int from, TextDiffLineKind kind)
    {
        var at = from;

        while (at < lines.Length && lines[at].Kind == kind)
        {
            at++;
        }

        return at;
    }

    /// <summary>
    /// Splits into words, leaving every character in exactly one piece so the pieces can be
    /// turned back into positions. A run of letters or digits is a word and everything else
    /// is one character of its own.
    /// </summary>
    private static List<TextDiffSpan> Words(string text)
    {
        List<TextDiffSpan> words = [];

        var at = 0;

        while (at < text.Length)
        {
            if (char.IsLetterOrDigit(text[at]) || text[at] == '_')
            {
                var start = at;

                while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_'))
                {
                    at++;
                }

                words.Add(new TextDiffSpan(start, at - start));
                continue;
            }

            words.Add(new TextDiffSpan(at, 1));
            at++;
        }

        return words;
    }

    private static int SharedHead(
        List<TextDiffSpan> before, List<TextDiffSpan> after, string oldText, string newText)
    {
        var count = 0;

        while (count < before.Count && count < after.Count
            && Same(oldText, before[count], newText, after[count]))
        {
            count++;
        }

        return count;
    }

    private static int SharedTail(
        List<TextDiffSpan> before, List<TextDiffSpan> after, string oldText, string newText, int head)
    {
        var count = 0;

        while (count < before.Count - head && count < after.Count - head
            && Same(oldText, before[^(count + 1)], newText, after[^(count + 1)]))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The part between the shared ends, compared word by word. The table is the ordinary
    /// longest common subsequence, and anything not on it is what changed.
    /// </summary>
    private static (List<TextDiffSpan> Old, List<TextDiffSpan> New) Middle(
        List<TextDiffSpan> before,
        List<TextDiffSpan> after,
        int head,
        int oldCount,
        int newCount,
        string oldText,
        string newText)
    {
        var common = new int[oldCount + 1, newCount + 1];

        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                common[i, j] = Same(oldText, before[head + i], newText, after[head + j])
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        List<TextDiffSpan> oldChanged = [];
        List<TextDiffSpan> newChanged = [];

        int left = 0, right = 0;

        while (left < oldCount && right < newCount)
        {
            if (Same(oldText, before[head + left], newText, after[head + right]))
            {
                left++;
                right++;
            }
            else if (common[left + 1, right] >= common[left, right + 1])
            {
                Add(oldChanged, before[head + left++]);
            }
            else
            {
                Add(newChanged, after[head + right++]);
            }
        }

        while (left < oldCount)
        {
            Add(oldChanged, before[head + left++]);
        }

        while (right < newCount)
        {
            Add(newChanged, after[head + right++]);
        }

        return (oldChanged, newChanged);
    }

    /// <summary>Adds a word, joining it to the run before it when they touch.</summary>
    private static void Add(List<TextDiffSpan> runs, TextDiffSpan word)
    {
        if (runs.Count > 0 && runs[^1].End == word.Start)
        {
            runs[^1] = new TextDiffSpan(runs[^1].Start, runs[^1].End + word.Length - runs[^1].Start);
            return;
        }

        runs.Add(word);
    }

    private static bool Same(string left, TextDiffSpan a, string right, TextDiffSpan b) =>
        a.Length == b.Length
        && left.AsSpan(a.Start, a.Length).SequenceEqual(right.AsSpan(b.Start, b.Length));

    private static double Covers(List<TextDiffSpan> runs, string text) =>
        text.Length == 0 ? 0 : (double)runs.Sum(r => r.Length) / text.Length;
}
