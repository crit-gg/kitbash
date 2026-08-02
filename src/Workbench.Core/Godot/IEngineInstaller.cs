namespace Workbench.Core.Godot;

/// <summary>
/// Fetches a build, checks it, unpacks it and registers it.
/// </summary>
/// <remarks>
/// <para>
/// **Several installs run at once**, behind a cap, and each is cancelled on its own. A
/// caller starts as many as it likes and the rest queue, so a page never has to serialise
/// anything itself. This is a change from the design, which runs one at a time and cancels
/// the running one when another starts.
/// </para>
/// <para>
/// Every step reports through <see cref="EngineInstallProgress"/> and every step obeys the
/// token, since a hundred megabytes and a hundred and fifty megabytes of unpacking are both
/// long enough to want stopping.
/// </para>
/// </remarks>
public interface IEngineInstaller
{
    /// <summary>
    /// Installs one build, replacing any install of the same version, and returns what
    /// landed.
    /// </summary>
    /// <exception cref="EngineInstallException">
    /// The checksum did not match, the release published none, the archive would not open,
    /// or what came out was not an engine. **A mismatch refuses rather than warns.**
    /// </exception>
    Task<InstalledEngine> InstallAsync(
        EngineBuild build,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);
}
