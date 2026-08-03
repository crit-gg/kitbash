using System.Diagnostics.CodeAnalysis;

namespace Kitbash.Core.Godot;

/// <summary>
/// What a Godot binary prints for <c>--version</c>, such as
/// <c>4.7.1.stable.mono.official.a13da4feb</c>.
/// </summary>
public readonly record struct EngineBuildString
{
    private EngineBuildString(string number, string status, string moduleConfig, string build, string commit)
    {
        Number = number;
        Status = status;
        ModuleConfig = moduleConfig;
        Build = build;
        Commit = commit;
    }

    /// <summary>The version, such as <c>4.7.1</c>. The patch is absent when it is zero.</summary>
    public string Number { get; }

    /// <summary>The channel with its number, such as <c>stable</c> or <c>dev2</c>.</summary>
    public string Status { get; }

    /// <summary>What the modules add, such as <c>mono</c>. Empty when there is none.</summary>
    public string ModuleConfig { get; }

    /// <summary><c>official</c> for a build Godot published, otherwise the builder's name.</summary>
    public string Build { get; }

    public string Commit { get; }

    public static EngineBuildString Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text, out var value))
        {
            throw new FormatException($"'{text}' is not a Godot build string.");
        }

        return value;
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out EngineBuildString value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split('.');
        var numbers = 0;

        while (numbers < parts.Length && IsDigits(parts[numbers]))
        {
            numbers++;
        }

        // Two numeric parts at least, since Godot has always printed a major and a minor.
        // Four covers the Godot 1 and 2 era, which spelled a fourth component.
        if (numbers is < 2 or > 4)
        {
            return false;
        }

        // A status, a build and a commit have to follow the version.
        if (parts.Length - numbers < 3)
        {
            return false;
        }

        value = new EngineBuildString(
            string.Join('.', parts[..numbers]),
            parts[numbers],
            string.Join('.', parts[(numbers + 1)..^2]),
            parts[^2],
            parts[^1]);

        return true;
    }

    public bool IsMono => ModuleConfig.Contains("mono", StringComparison.Ordinal);

    public bool IsOfficial => Build == "official";

    /// <summary>
    /// The folder Godot keeps this version's export templates in, under its own data
    /// directory. Those are 1.9 GB a set and Kitbash does not manage them.
    /// </summary>
    public string ExportTemplateFolder =>
        ModuleConfig.Length == 0 ? $"{Number}.{Status}" : $"{Number}.{Status}.{ModuleConfig}";

    /// <summary>
    /// The release tag this build came from. False for a build whose status is not a
    /// channel Godot publishes, which is possible for a custom build.
    /// </summary>
    public bool TryGetTag(out EngineTag tag) => EngineTag.TryParse($"{Number}-{Status}", out tag);

    /// <summary>
    /// The name this install goes by. False for the same reason as
    /// <see cref="TryGetTag"/>, and such an install is known by its path instead.
    /// </summary>
    public bool TryGetId(out EngineId id)
    {
        if (!TryGetTag(out var tag))
        {
            id = default;

            return false;
        }

        id = new EngineId(tag, IsMono);

        return true;
    }

    public override string ToString() =>
        ModuleConfig.Length == 0
            ? $"{Number}.{Status}.{Build}.{Commit}"
            : $"{Number}.{Status}.{ModuleConfig}.{Build}.{Commit}";

    private static bool IsDigits(string part)
    {
        if (part.Length == 0)
        {
            return false;
        }

        foreach (var c in part)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
