namespace Kitbash.Core.Godot;

/// <summary>
/// Export templates for an installed engine, in the folder Godot itself reads them from.
/// Never installed unless somebody asks.
/// </summary>
public interface IEngineTemplates
{
    /// <summary>
    /// Fetches, checks and unpacks the templates the engine's release published, replacing
    /// any already in that folder, and records it on the engine. Returns the engine as it
    /// now reads.
    /// </summary>
    /// <exception cref="EngineInstallException">
    /// The release published none, the file did not match its hash, or it would not unpack.
    /// </exception>
    /// <exception cref="EngineCatalogueException">The release could not be read.</exception>
    Task<InstalledEngine> InstallAsync(
        InstalledEngine engine,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// The templates folder Kitbash installed for this engine that nothing else still uses,
    /// or null. Removing the engine should remove this too.
    /// </summary>
    string? OwnedBy(InstalledEngine engine, IReadOnlyList<InstalledEngine> installed);

    /// <summary>Bytes in a templates folder, or zero when it is not there.</summary>
    long SizeOf(string folder);

    /// <summary>Deletes a templates folder by name. A folder that is not there is not an error.</summary>
    void Remove(string folder);
}
