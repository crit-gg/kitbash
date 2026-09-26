namespace Kitbash.Core.Godot;

/// <summary>
/// What Kitbash knows about one install that it cannot work out again cheaply or at all.
/// </summary>
/// <param name="Id">The tag and the .NET flag, which name the install.</param>
/// <param name="Platform">The platform the build was for.</param>
/// <param name="Architecture">
/// The processor the build was for. Nothing else records it. A build string does not
/// carry it and neither does the folder name.
/// </param>
/// <param name="BuildString">What the binary printed for <c>--version</c>.</param>
/// <param name="Executable">The editor, relative to the install directory.</param>
/// <param name="SourceFileName">The archive this came from, or empty when imported.</param>
/// <param name="Checksum">The SHA 512 verified at install, or empty when imported.</param>
/// <param name="InstalledAt">When it landed. Nothing else on disk says.</param>
public sealed record EngineRecord(
    EngineId Id,
    EnginePlatform Platform,
    EngineArchitecture Architecture,
    string BuildString,
    string Executable,
    string SourceFileName,
    string Checksum,
    DateTimeOffset InstalledAt)
{
    /// <summary>The file name a record takes inside an install directory.</summary>
    public const string FileName = ".kitbash-engine.toml";

    /// <summary>True when this describes a build Kitbash fetched rather than one found.</summary>
    public bool IsInstalled => SourceFileName.Length > 0;

    /// <summary>Which hash <see cref="Checksum"/> is. Official builds publish SHA 512.</summary>
    public EngineChecksumKind ChecksumKind { get; init; } = EngineChecksumKind.Sha512;

    /// <summary>
    /// The newest pin this install answers, such as <c>4.7.2-mono</c>, or empty for an
    /// install that never moves. A slot is replaced whole when its repository publishes a
    /// newer build.
    /// </summary>
    public string Slot { get; init; } = string.Empty;

    /// <summary>
    /// The export templates folder Kitbash installed for this engine, such as
    /// <c>4.7.2.slopworks.mono</c>, or empty when it installed none.
    /// </summary>
    public string Templates { get; init; } = string.Empty;

    /// <summary>
    /// The release this was installed from, as its source names it, such as
    /// <c>4.7.2-slopworks-18d5d19</c>. Empty for an engine that was found, not installed.
    /// </summary>
    public string Release { get; init; } = string.Empty;
}
