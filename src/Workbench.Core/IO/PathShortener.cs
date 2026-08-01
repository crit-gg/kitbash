namespace Workbench.Core.IO;

/// <summary>
/// The elision every platform shares. A subclass supplies the separator, the display
/// form and the root, which is everything that differs.
/// </summary>
internal abstract class PathShortener : IPathShortener
{
    private const string Elision = "...";

    public string Shorten(string path, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var display = ToDisplay(path);

        if (display.Length <= maxLength)
        {
            return display;
        }

        var root = RootOf(display);
        var segments = display[root.Length..].Split(Separator, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= 1)
        {
            return display;
        }

        // Without a root the first segment stands in for one, so it survives the elision.
        var head = root.Length > 0 ? root : segments[0] + Separator;
        var available = root.Length > 0 ? segments.Length : segments.Length - 1;

        // Take as much of the tail as fits, always at least the last segment.
        for (var take = available - 1; take >= 1; take--)
        {
            var candidate = head + Elision + Separator + string.Join(Separator, segments[^take..]);

            if (candidate.Length <= maxLength)
            {
                return candidate;
            }
        }

        return segments[^1];
    }

    /// <summary>The separator this platform shows.</summary>
    protected abstract char Separator { get; }

    /// <summary>Rewrites a path the way this platform writes one, before any elision.</summary>
    protected abstract string ToDisplay(string path);

    /// <summary>
    /// The leading part that must survive, including its trailing separator, or empty
    /// when the path has none.
    /// </summary>
    protected abstract string RootOf(string display);
}
