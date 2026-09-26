using System.Diagnostics.CodeAnalysis;

namespace Kitbash.Core.Godot;

/// <summary>
/// The name of one install: where it came from, a release tag, the build and whether it is
/// the .NET build. An official install reads <c>4.7.1-stable-mono</c> and one from a
/// repository reads <c>github/crit-gg/godot-slopworks/4.7.2+18d5d19-mono</c>.
/// </summary>
public readonly record struct EngineId(EngineTag Tag, bool IsMono) : IComparable<EngineId>
{
    private const string MonoSuffix = "-mono";

    /// <summary>Null for a build the Godot project published.</summary>
    public EngineRepositoryAddress? Repository { get; init; }

    /// <summary>
    /// The last part of a repository's release tag, such as <c>18d5d19</c>. Null for an
    /// official build, whose tag already names one release.
    /// </summary>
    public string? Build { get; init; }

    public bool IsOfficial => Repository is null;

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
        var slash = rest.LastIndexOf('/');

        if (slash < 0)
        {
            if (!EngineTag.TryParse(rest, out var tag) || tag.Channel == EngineChannel.Custom)
            {
                return false;
            }

            id = new EngineId(tag, mono);

            return true;
        }

        if (!EngineRepositoryAddress.TryParse(rest[..slash], out var repository))
        {
            return false;
        }

        var version = rest[(slash + 1)..];
        var plus = version.IndexOf('+', StringComparison.Ordinal);

        if (plus < 0
            || !EngineTag.TryParseNumber(version[..plus], out var number)
            || !IsBuild(version[(plus + 1)..]))
        {
            return false;
        }

        id = new EngineId(number, mono) { Repository = repository, Build = version[(plus + 1)..] };

        return true;
    }

    /// <summary>
    /// What a repository's build part may hold. No dash, since a build is the last part of a
    /// tag after one, and nothing a folder name or this type's text form would trip on.
    /// </summary>
    public static bool IsBuild([NotNullWhen(true)] string? text) =>
        !string.IsNullOrEmpty(text) && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.');

    /// <summary>
    /// The folder an official install is unpacked into, under the engine directory. The
    /// text form is used as written, since every character in it is legal in a file name
    /// on every platform. A repository install is placed by the installer instead.
    /// </summary>
    public string DirectoryName => ToString();

    /// <summary>The version and build alone, such as <c>4.7.2+18d5d19</c>, without the repository.</summary>
    public string VersionText => Repository is null
        ? Tag.ToString()
        : $"{NumberOf(Tag)}+{Build}";

    public int CompareTo(EngineId other)
    {
        var by = Tag.CompareTo(other.Tag);

        if (by != 0)
        {
            return by;
        }

        by = IsMono.CompareTo(other.IsMono);

        if (by != 0)
        {
            return by;
        }

        by = string.CompareOrdinal(Repository?.ToString(), other.Repository?.ToString());

        return by != 0 ? by : string.CompareOrdinal(Build, other.Build);
    }

    public override string ToString()
    {
        var text = Repository is null ? Tag.ToString() : $"{Repository}/{VersionText}";

        return IsMono ? text + MonoSuffix : text;
    }

    /// <summary>The numbers alone, such as <c>4.7.2</c>, as Godot writes them.</summary>
    public static string NumberOf(EngineTag tag) =>
        tag.Patch > 0 ? $"{tag.Major}.{tag.Minor}.{tag.Patch}" : $"{tag.Major}.{tag.Minor}";

    public static bool operator <(EngineId left, EngineId right) => left.CompareTo(right) < 0;

    public static bool operator >(EngineId left, EngineId right) => left.CompareTo(right) > 0;

    public static bool operator <=(EngineId left, EngineId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(EngineId left, EngineId right) => left.CompareTo(right) >= 0;
}
