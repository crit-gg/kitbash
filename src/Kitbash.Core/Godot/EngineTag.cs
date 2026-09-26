using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Kitbash.Core.Godot;

/// <summary>
/// A Godot release tag, such as <c>4.7.1-stable</c> or <c>4.8-dev2</c>. Parsing is the
/// only way to make one, so a value of this type has already been checked.
/// </summary>
public readonly partial record struct EngineTag : IComparable<EngineTag>
{
    private EngineTag(int major, int minor, int patch, EngineChannel channel, int number)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Channel = channel;
        Number = number;
    }

    public int Major { get; }

    public int Minor { get; }

    /// <summary>Zero when the release carries no patch, which is how Godot spells zero.</summary>
    public int Patch { get; }

    public EngineChannel Channel { get; }

    /// <summary>The number after the channel. Zero for stable, which never carries one.</summary>
    public int Number { get; }

    public static EngineTag Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text, out var tag))
        {
            throw new FormatException($"'{text}' is not a Godot release tag.");
        }

        return tag;
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out EngineTag tag)
    {
        tag = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Grammar().Match(text);

        if (!match.Success)
        {
            return false;
        }

        var channel = match.Groups[4].Value switch
        {
            "stable" => EngineChannel.Stable,
            "custom" => EngineChannel.Custom,
            "rc" => EngineChannel.Rc,
            "beta" => EngineChannel.Beta,
            "alpha" => EngineChannel.Alpha,
            _ => EngineChannel.Dev,
        };

        var hasNumber = match.Groups[5].Success;

        // Stable and custom never carry a number and everything else always does. A tag
        // breaking that is not one Godot published.
        if (hasNumber == (channel is EngineChannel.Stable or EngineChannel.Custom))
        {
            return false;
        }

        tag = new EngineTag(
            int.Parse(match.Groups[1].ValueSpan),
            int.Parse(match.Groups[2].ValueSpan),
            match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan) : 0,
            channel,
            hasNumber ? int.Parse(match.Groups[5].ValueSpan) : 0);

        return true;
    }

    /// <summary>
    /// False for Godot 3 and below, which Kitbash does not offer or install. Their
    /// naming carries per version overrides for most of the 1.x and 2.x range and a
    /// different spelling for Linux and macOS, and none of it is worth carrying. The
    /// grammar still accepts them, so this is where the scope is stated.
    /// </summary>
    public bool IsSupported => Major >= 4;

    /// <summary>
    /// The base version of a repository build, such as <c>4.7.2</c> read out of the release
    /// tag <c>4.7.2-slopworks-18d5d19</c>. The tag's own name and build are not part of it.
    /// </summary>
    public static bool TryParseNumber([NotNullWhen(true)] string? text, out EngineTag tag)
    {
        tag = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // A zero patch is allowed here, since a fork's workflow may print 4.7.0 where Godot
        // itself would print 4.7.
        var match = NumberGrammar().Match(text);

        if (!match.Success)
        {
            return false;
        }

        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan) : 0;

        tag = new EngineTag(
            int.Parse(match.Groups[1].ValueSpan),
            int.Parse(match.Groups[2].ValueSpan),
            patch,
            EngineChannel.Custom,
            0);

        return true;
    }

    /// <summary>The channel as Godot spells it, such as <c>rc</c>.</summary>
    public string ChannelText => Channel switch
    {
        EngineChannel.Stable => "stable",
        EngineChannel.Custom => "custom",
        EngineChannel.Rc => "rc",
        EngineChannel.Beta => "beta",
        EngineChannel.Alpha => "alpha",
        _ => "dev",
    };

    public int CompareTo(EngineTag other)
    {
        var by = Major.CompareTo(other.Major);

        if (by != 0)
        {
            return by;
        }

        by = Minor.CompareTo(other.Minor);

        if (by != 0)
        {
            return by;
        }

        by = Patch.CompareTo(other.Patch);

        if (by != 0)
        {
            return by;
        }

        by = Channel.CompareTo(other.Channel);

        return by != 0 ? by : Number.CompareTo(other.Number);
    }

    public override string ToString()
    {
        var number = Patch > 0 ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}";

        return Channel is EngineChannel.Stable or EngineChannel.Custom
            ? $"{number}-{ChannelText}"
            : $"{number}-{ChannelText}{Number}";
    }

    public static bool operator <(EngineTag left, EngineTag right) => left.CompareTo(right) < 0;

    public static bool operator >(EngineTag left, EngineTag right) => left.CompareTo(right) > 0;

    public static bool operator <=(EngineTag left, EngineTag right) => left.CompareTo(right) <= 0;

    public static bool operator >=(EngineTag left, EngineTag right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^(\d+)\.(\d+)(?:\.(\d+))?$")]
    private static partial Regex NumberGrammar();

    // The channel list is spelled out rather than left as letters, so an unknown label is
    // refused here instead of arriving at EngineChannel with no rank.
    [GeneratedRegex(@"^(\d+)\.(\d+)(?:\.([1-9]\d*))?-(stable|custom|dev|alpha|beta|rc)(\d+)?$")]
    private static partial Regex Grammar();
}
