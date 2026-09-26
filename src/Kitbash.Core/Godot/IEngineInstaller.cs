namespace Kitbash.Core.Godot;

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

    /// <summary>
    /// Unpacks a repository build beside its slot, where it waits to be placed. Replaces
    /// anything already waiting there.
    /// </summary>
    /// <exception cref="EngineInstallException">As <see cref="InstallAsync"/>.</exception>
    Task<InstalledEngine> StageAsync(
        EngineBuild build,
        string slot,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>The build waiting to be placed in a slot, or null when none is.</summary>
    InstalledEngine? Staged(EngineRepositoryAddress address, string slot);

    /// <summary>
    /// Moves the waiting build into its slot and removes the one it replaces. Null when
    /// nothing was waiting. The caller checks the old one is not running first.
    /// </summary>
    /// <exception cref="IOException">The slot is in use, so nothing moved.</exception>
    InstalledEngine? Place(EngineRepositoryAddress address, string slot);
}
