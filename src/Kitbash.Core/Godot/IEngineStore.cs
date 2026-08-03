namespace Kitbash.Core.Godot;

/// <summary>
/// The engines on this machine. Lists what is under the engine directory, plus the ones a
/// person pointed at, and probes anything whose record is missing or stale.
/// </summary>
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
    Task<InstalledEngine?> ImportAsync(string directory, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an engine out of the list. Deletes the files only for an install Kitbash
    /// made. An imported engine is forgotten and left where it is.
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
