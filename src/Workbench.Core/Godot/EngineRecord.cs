namespace Workbench.Core.Godot;

/// <summary>
/// What Workbench knows about one install that it cannot work out again cheaply or at all.
/// </summary>
/// <remarks>
/// <para>
/// Written once when an engine is installed or imported, and read on every refresh
/// afterwards. Without it, listing installs means running <c>--version</c> per engine and
/// hunting the editor in a tree whose layout differs per platform and per .NET flag.
/// </para>
/// <para>
/// **It is a cache of facts and never the authority.** A directory changes under an app.
/// A record that is missing, will not parse, or names an executable that is gone falls
/// back to probing and is written again.
/// </para>
/// </remarks>
/// <param name="Id">The tag and the .NET flag, which name the install.</param>
/// <param name="Platform">The platform the build was for.</param>
/// <param name="Architecture">
/// The processor the build was for. **The one fact nothing else can recover**, since a
/// build string does not carry it and the folder name does not either.
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
    /// <remarks>
    /// The dot hides it on Unix. Windows needs the attribute set as well, which is
    /// <see cref="IEngineFiles.Hide"/>.
    /// </remarks>
    public const string FileName = ".workbench-engine.toml";

    /// <summary>True when this describes a build Workbench fetched rather than one found.</summary>
    public bool IsInstalled => SourceFileName.Length > 0;
}
