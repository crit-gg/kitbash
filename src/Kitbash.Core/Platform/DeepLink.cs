using System.Diagnostics.CodeAnalysis;

namespace Kitbash.Core.Platform;

/// <summary>
/// A <c>kitbash</c> link, taken apart. Parsing is the only way to make one, so a value of
/// this type came from something that really is a link. It says nothing about what a verb
/// means, which is the application's to decide.
/// </summary>
/// <example>
/// <c>DeepLink.TryParse("kitbash://engine/4.7.1", out var link)</c> gives the verb
/// <c>engine</c> and one segment, <c>4.7.1</c>.
/// </example>
public sealed class DeepLink
{
    /// <summary>The scheme every desktop registers Kitbash for.</summary>
    public const string Scheme = "kitbash";

    /// <summary>
    /// The most a link may be. A desktop hands over whatever a page asked for, so the
    /// length is somebody else's to choose and this is where it stops.
    /// </summary>
    private const int LengthLimit = 2048;

    private readonly IReadOnlyDictionary<string, string> _query;

    private DeepLink(
        string verb, IReadOnlyList<string> segments, IReadOnlyDictionary<string, string> query)
    {
        Verb = verb;
        Segments = segments;
        _query = query;
    }

    /// <summary>What the link asks for, lower case. Never empty.</summary>
    public string Verb { get; }

    /// <summary>The path after the verb, each part unescaped. Often empty.</summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>The first segment, or null when the link carries none.</summary>
    public string? First => Segments.Count > 0 ? Segments[0] : null;

    /// <summary>One query value, unescaped, or null when the link does not carry it.</summary>
    public string? Value(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _query.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// True when the text is a link. Both shapes a desktop may hand over are taken, the
    /// one with an authority such as <c>kitbash://engine/4.7.1</c> and the one without.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out DeepLink? link)
    {
        link = null;

        if (text is null || text.Length is 0 or > LengthLimit)
        {
            return false;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        List<string> parts =
        [
            .. uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString),
        ];

        // Uri puts the first word in the host when the link has an authority and in the
        // path when it does not, and a browser may hand over either.
        var verb = uri.Host;

        if (verb.Length == 0)
        {
            if (parts.Count == 0)
            {
                return false;
            }

            verb = parts[0];
            parts.RemoveAt(0);
        }

        if (verb.Length == 0)
        {
            return false;
        }

        link = new DeepLink(verb.ToLowerInvariant(), parts, Query(uri.Query));

        return true;
    }

    /// <summary>
    /// The query as names and values. A repeated name keeps the first, since a second one
    /// is either a mistake or somebody trying to talk past a check.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Query(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=', StringComparison.Ordinal);

            if (split <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(pair[..split]);

            if (name.Length > 0 && !values.ContainsKey(name))
            {
                values[name] = Uri.UnescapeDataString(pair[(split + 1)..]);
            }
        }

        return values;
    }
}
