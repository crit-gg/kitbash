namespace Workbench.Core.Godot;

/// <summary>
/// Fetches a build, checks it, unpacks it and registers it.
/// </summary>
public interface IEngineInstaller
{
    /// <summary>
    /// Installs one build, replacing any install of the same version, and returns what
    /// landed.
    /// </summary>
    /// <exception cref="EngineInstallException">
    /// The checksum did not match, the release published none, the archive would not open,
    /// or what came out was not an engine. A mismatch refuses, it never warns and proceeds.
    /// </exception>
    Task<InstalledEngine> InstallAsync(
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);
}
