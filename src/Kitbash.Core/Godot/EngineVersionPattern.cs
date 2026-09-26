using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Kitbash.Core.Godot;

/// <summary>
/// A version a project asks for, which is looser than a tag. Every part after the major
/// is optional and a part left out matches anything.
/// </summary>
public readonly partial record struct EngineVersionPattern
{
    private EngineVersionPattern(
        int major, int? minor, int? patch, EngineChannel? channel, int? number, bool needsDotnet, string? build)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Channel = channel;
        Number = number;
        NeedsDotnet = needsDotnet;
        Build = build;
    }

    public int Major { get; }

    public int? Minor { get; }

    public int? Patch { get; }

    public EngineChannel? Channel { get; }

    /// <summary>The number after the channel, when the pattern is as exact as <c>4.8-rc1</c>.</summary>
    public int? Number { get; }

    /// <summary>
    /// True when the pattern ended in <c>-mono</c>, which asks for the .NET build.
    /// </summary>
    public bool NeedsDotnet { get; }

    /// <summary>
    /// The build a repository pin names after a plus, such as <c>18d5d19</c>. Null for a
    /// newest pin, which follows whatever the repository publishes next.
    /// </summary>
    public string? Build { get; }

    /// <summary>True for a repository pin that names one build, which is never replaced.</summary>
    public bool IsExact => Build is not null;

    /// <summary>The same scope rule <see cref="EngineTag.IsSupported"/> states.</summary>
    public bool IsSupported => Major >= 4;

    public static EngineVersionPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text, out var pattern))
        {
            throw new FormatException($"'{text}' is not a Godot version.");
        }

        return pattern;
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out EngineVersionPattern pattern)
    {
        pattern = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Grammar().Match(text.Trim());

        // A build belongs to a repository pin, which is read by TryParseForRepository.
        if (match.Success && match.Groups[6].Success)
        {
            return false;
        }

        if (!match.Success)
        {
            return false;
        }

        EngineChannel? channel = match.Groups[4].Success
            ? match.Groups[4].Value switch
            {
                "stable" => EngineChannel.Stable,
                "rc" => EngineChannel.Rc,
                "beta" => EngineChannel.Beta,
                "alpha" => EngineChannel.Alpha,
                _ => EngineChannel.Dev,
            }
            : null;

        int? number = match.Groups[5].Success ? int.Parse(match.Groups[5].ValueSpan) : null;
        var needsDotnet = match.Groups[7].Success;

        // A .NET build is named by its channel, so there is no such thing as 4.7-mono.
        if (needsDotnet && channel is null)
        {
            return false;
        }

        // Stable never carries a number, the same rule the tag grammar enforces.
        if (channel == EngineChannel.Stable && number is not null)
        {
            return false;
        }

        pattern = new EngineVersionPattern(
            int.Parse(match.Groups[1].ValueSpan),
            match.Groups[2].Success ? int.Parse(match.Groups[2].ValueSpan) : null,
            match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan) : null,
            channel,
            number,
            needsDotnet,
            null);

        return true;
    }

    /// <summary>
    /// A pin for a workspace that names a repository: <c>4.7.2</c> follows the newest
    /// build and <c>4.7.2+18d5d19</c> names one. Every repository build is custom, so a
    /// channel is refused, and <c>-mono</c> may end either form.
    /// </summary>
    public static bool TryParseForRepository([NotNullWhen(true)] string? text, out EngineVersionPattern pattern)
    {
        pattern = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Grammar().Match(text.Trim());

        if (!match.Success || match.Groups[4].Success)
        {
            return false;
        }

        var build = match.Groups[6].Success ? match.Groups[6].Value : null;

        // A build pins one release, so the version beside it has to be as exact as a tag.
        if (build is not null && !match.Groups[2].Success)
        {
            return false;
        }

        pattern = new EngineVersionPattern(
            int.Parse(match.Groups[1].ValueSpan),
            match.Groups[2].Success ? int.Parse(match.Groups[2].ValueSpan) : null,
            match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan) : null,
            EngineChannel.Custom,
            null,
            match.Groups[7].Success,
            build);

        return true;
    }

    /// <summary>Whether a release answers this. Every part that is set has to be equal.</summary>
    public bool Matches(EngineTag tag) =>
        Major == tag.Major
        && (Minor is not { } minor || minor == tag.Minor)
        && (Patch is not { } patch || patch == tag.Patch)
        && (Channel is not { } channel || channel == tag.Channel)
        && (Number is not { } number || number == tag.Number);

    /// <summary>
    /// Whether an install answers this, repository and build included. The runtime is the
    /// caller's to check, since a project can ask for .NET when the pin does not.
    /// </summary>
    public bool Matches(EngineId id) =>
        Matches(id.Tag) && (Build is null || string.Equals(Build, id.Build, StringComparison.Ordinal));

    /// <summary>
    /// The best release in a list for this pattern, or null when none answers it.
    /// </summary>
    public EngineTag? BestMatch(IEnumerable<EngineTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        EngineTag? best = null;

        foreach (var tag in tags)
        {
            if (Matches(tag) && (best is not { } found || Beats(tag, found)))
            {
                best = tag;
            }
        }

        return best;
    }

    private bool Beats(EngineTag candidate, EngineTag holder)
    {
        if (Channel is null)
        {
            var candidateIsStable = candidate.Channel == EngineChannel.Stable;

            if (candidateIsStable != (holder.Channel == EngineChannel.Stable))
            {
                return candidateIsStable;
            }
        }

        return candidate > holder;
    }

    public override string ToString()
    {
        var text = Major.ToString();

        if (Minor is { } minor)
        {
            text += $".{minor}";
        }

        if (Patch is { } patch)
        {
            text += $".{patch}";
        }

        if (Channel is { } channel && channel != EngineChannel.Custom)
        {
            text += $"-{ChannelText(channel)}{Number}";
        }

        if (Build is { } build)
        {
            text += $"+{build}";
        }

        return NeedsDotnet ? text + "-mono" : text;
    }

    private static string ChannelText(EngineChannel channel) => channel switch
    {
        EngineChannel.Stable => "stable",
        EngineChannel.Rc => "rc",
        EngineChannel.Beta => "beta",
        EngineChannel.Alpha => "alpha",
        _ => "dev",
    };

    // A patch cannot appear without a minor, which the nesting enforces. Unlike the tag
    // grammar a zero patch is allowed here, since a person writes this. The -mono tail
    // makes it the same grammar an install is named by.
    [GeneratedRegex(@"^(\d+)(?:\.(\d+)(?:\.(\d+))?)?(?:-(stable|dev|alpha|beta|rc)(\d+)?)?(?:\+([0-9A-Za-z_.]+))?(-mono)?$")]
    private static partial Regex Grammar();
}
