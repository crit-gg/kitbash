using System.Diagnostics.CodeAnalysis;

namespace Workbench.Core.Godot;

/// <summary>
/// The name of one install: a release tag and whether it is the .NET build. Its text form
/// is <c>4.7.1-stable</c> or <c>4.7.1-stable-mono</c>.
/// </summary>
public readonly record struct EngineId(EngineTag Tag, bool IsMono) : IComparable<EngineId>
{
    private const string MonoSuffix = "-mono";

    public static EngineId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text, out var id))
        {
            throw new FormatException($"'{text}' is not a Godot engine name.");
        }

        return id;
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out EngineId id)
    {
        id = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var mono = text.EndsWith(MonoSuffix, StringComparison.Ordinal);
        var rest = mono ? text[..^MonoSuffix.Length] : text;

        if (!EngineTag.TryParse(rest, out var tag))
        {
            return false;
        }

        id = new EngineId(tag, mono);

        return true;
    }

    /// <summary>
    /// The folder this install is unpacked into, under the engine directory. The text form
    /// is used as written, since every character in it is legal in a file name on both
    /// platforms.
    /// </summary>
    public string DirectoryName => ToString();

    public int CompareTo(EngineId other)
    {
        var by = Tag.CompareTo(other.Tag);

        return by != 0 ? by : IsMono.CompareTo(other.IsMono);
    }

    public override string ToString() => IsMono ? $"{Tag}{MonoSuffix}" : Tag.ToString();

    public static bool operator <(EngineId left, EngineId right) => left.CompareTo(right) < 0;

    public static bool operator >(EngineId left, EngineId right) => left.CompareTo(right) > 0;

    public static bool operator <=(EngineId left, EngineId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(EngineId left, EngineId right) => left.CompareTo(right) >= 0;
}
