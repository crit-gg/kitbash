namespace Workbench.Core.Godot;

/// <summary>
/// The engines on this machine. Lists what is under the engine directory, plus the ones a
/// person pointed at, and probes anything whose record is missing or stale.
/// </summary>
/// <remarks>
/// This is the workspace registry's shape. Only the paths are stored, and the names, the
/// sizes and whether a folder is still there are read from disk on every refresh, so a
/// renamed or deleted engine shows up without anyone maintaining a list.
/// </remarks>
public interface IEngineStore
{
    /// <summary>
    /// Every engine, newest release first. Touches a disk and runs a process, so it never
    /// belongs on the UI thread.
    /// </summary>
    Task<IReadOnlyList<InstalledEngine>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds an engine already on this machine, given its directory. The editor inside is
    /// found and asked its version, and null comes back when nothing there is one.
    /// </summary>
    /// <remarks>
    /// **The record for an imported engine is kept in application state**, not written into
    /// the folder. Workbench did not create that directory, and writing into a folder
    /// someone else owns is the same overstep as deleting it.
    /// </remarks>
    Task<InstalledEngine?> ImportAsync(string directory, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an engine out of the list. **Deletes the files only when Workbench installed
    /// them.** An imported engine is forgotten and left where it is.
    /// </summary>
    Task RemoveAsync(InstalledEngine engine, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the record for an engine that has just been unpacked, which is how the
    /// installer registers one.
    /// </summary>
    Task<InstalledEngine> RegisterAsync(
        string directory,
        EngineBuild build,
        string checksum,
        CancellationToken cancellationToken);
}
