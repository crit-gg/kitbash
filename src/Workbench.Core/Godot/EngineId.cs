using System.Diagnostics.CodeAnalysis;

namespace Workbench.Core.Godot;

/// <summary>
/// The name of one install: a release tag and whether it is the .NET build. Its text form
/// is <c>4.7.1-stable</c> or <c>4.7.1-stable-mono</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three things take this. The setting that names the machine default, the folder an
/// install is unpacked into, and the key a workspace pins its engine with. That last one
/// is why the text form matters, since a person types it into a config file.
/// </para>
/// <para>
/// **One install per version, so the processor is not in the name.** Other architectures
/// are installable, but a machine holds at most one build of a given tag and .NET flag, so
/// installing another processor's build of a version already present replaces it rather
/// than sitting beside it. That keeps a pin readable and portable: a workspace pinning
/// <c>4.7.1-stable-mono</c> means the same thing on every machine, and each one holds
/// whichever build it installed.
/// </para>
/// <para>
/// Which processor an install actually is gets recorded against the install rather than
/// encoded here, since nothing needs to name it and a build string does not carry it.
/// </para>
/// <para>
/// The .NET build is a flag on a version and never part of the version. Two installs of
/// one tag can sit side by side, one with it and one without.
/// </para>
/// <para>
/// The suffix parses without ambiguity even though the tag already holds a hyphen, because
/// a channel is letters with no hyphen of its own. So the last hyphen can only be the flag.
/// Parse the tag first and take what is left. The word is <c>mono</c> because that is what
/// Godot calls the download everywhere it names it.
/// </para>
/// </remarks>
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
