using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Workbench.Core.Godot;

/// <summary>
/// A version a project asks for, which is looser than a tag. Every part after the major
/// is optional and a part left out matches anything.
/// </summary>
/// <remarks>
/// <para>
/// This exists because what a project names is not a release. <c>config/features</c>
/// carries <c>4.7</c>, and the release that answers it could be <c>4.7-stable</c> or
/// <c>4.7.1-stable</c>. An <see cref="EngineTag"/> cannot hold that, since a tag always
/// knows its patch and its channel, so a requirement needs a type of its own.
/// </para>
/// <para>
/// The .NET flag is deliberately not here. An engine's identity is a tag plus that flag,
/// and the two are answered by different questions: the version comes from what a project
/// pins and the flag from whether the project has C# in it. Mixing them into one pattern
/// makes both harder to read.
/// </para>
/// <para>
/// **A pattern cannot spell one exact release the way a tag does, and it is not meant
/// to.** A tag writes a zero patch as nothing, so <c>4.7-stable</c> means patch zero,
/// while here a missing patch means any patch and only <c>4.7.0-stable</c> pins it to
/// zero. The two spellings cannot be reconciled, so anything that means one specific
/// release holds an <see cref="EngineTag"/> instead. This type is for the loose case,
/// which is the only case a project file produces.
/// </para>
/// </remarks>
public readonly partial record struct EngineVersionPattern
{
    private EngineVersionPattern(int major, int? minor, int? patch, EngineChannel? channel, int? number)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Channel = channel;
        Number = number;
    }

    public int Major { get; }

    public int? Minor { get; }

    public int? Patch { get; }

    public EngineChannel? Channel { get; }

    /// <summary>The number after the channel, when the pattern is as exact as <c>4.8-rc1</c>.</summary>
    public int? Number { get; }

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
            number);

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
    /// The best release in a list for this pattern, or null when none answers it.
    /// </summary>
    /// <remarks>
    /// **A pattern that names no channel prefers a stable release over any prerelease**,
    /// whatever the version numbers say, and only then takes the highest. So <c>4.7</c>
    /// with 4.7.1-stable and 4.7.2-rc1 installed opens the stable one. Taking the highest
    /// outright is the obvious rule and it is wrong here: a candidate is not an upgrade
    /// from a release, and somebody who wants one names it.
    ///
    /// A pattern that does name a channel has already said so, so the highest wins.
    /// </remarks>
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

        if (Channel is { } channel)
        {
            text += $"-{ChannelText(channel)}{Number}";
        }

        return text;
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
    // grammar a zero patch is allowed, since this is what a person writes rather than
    // what Godot published, and 4.7.0 plainly means 4.7.
    [GeneratedRegex(@"^(\d+)(?:\.(\d+)(?:\.(\d+))?)?(?:-(stable|dev|alpha|beta|rc)(\d+)?)?$")]
    private static partial Regex Grammar();
}
