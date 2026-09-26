namespace Kitbash.Core.Godot;

/// <summary>
/// Installs what a repository pin asks for and keeps a newest pin's slot current. Touches
/// the network, a disk and the process list, so none of it belongs on the UI thread.
/// </summary>
public interface IEngineUpdater
{
    /// <summary>
    /// The build a repository pin wants on this machine: the newest release that is not a
    /// prerelease for a newest pin, or the named build for an exact one. Null when the
    /// repository publishes nothing that answers.
    /// </summary>
    /// <exception cref="EngineCatalogueException">The repository could not be read.</exception>
    Task<EngineBuild?> FindAsync(EngineRequirement requirement, bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// Installs a build where the requirement says it belongs: its slot for a newest pin,
    /// its own folder otherwise.
    /// </summary>
    /// <exception cref="EngineInstallException">It could not be installed.</exception>
    /// <exception cref="IOException">The slot is in use.</exception>
    Task<InstalledEngine> InstallAsync(
        EngineRequirement requirement,
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Brings a newest pin's slot up to date. A newer build is unpacked beside the slot and
    /// takes it over at once, or when the engine in it has stopped running. Templates
    /// installed for the old build are installed for the new one.
    /// </summary>
    /// <param name="refresh">Ask the repository now rather than trusting a recent list.</param>
    /// <exception cref="EngineCatalogueException">The repository could not be read.</exception>
    /// <exception cref="EngineInstallException">The newer build could not be installed.</exception>
    /// <param name="progress">Reports the download of a newer build, when there is one.</param>
    Task<EngineUpdate> UpdateAsync(
        EngineRequirement requirement,
        bool refresh,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);
}
