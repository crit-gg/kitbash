namespace Workbench.Core.IO;

/// <summary>
/// Tells a caller that something in one of a few directories changed. Behind an interface
/// like the rest of filesystem access, so a test can raise a change without touching a disk.
/// </summary>
public interface IDirectoryWatcher : IDisposable
{
    /// <summary>
    /// The directories being watched, in the order they were asked for. Empty when nothing
    /// is. Goes back to empty on its own when a watch dies, so a caller that keeps this in
    /// step can put it back.
    /// </summary>
    IReadOnlyList<string> Watching { get; }

    /// <summary>
    /// Raised after something in a watched directory changed. Not on the calling thread, and
    /// not once per file: a caller that touches the interface has to marshal and expect
    /// bursts.
    /// </summary>
    event EventHandler<DirectoryChangedEventArgs>? Changed;

    /// <summary>
    /// Starts watching, replacing whatever was being watched. A directory that is not there,
    /// or that cannot be watched, is skipped rather than throwing, because a workspace can
    /// be on a share or a filesystem with no notifications and the app still has to run.
    /// </summary>
    /// <remarks>
    /// Subdirectories are excluded and there is no option to include them. A recursive watch
    /// costs one kernel handle per directory underneath, which on Linux is drawn from a per
    /// user pool that other applications share, and it turns one write of many files into
    /// one notification per file. Name the few folders that matter and poll for the rest.
    /// </remarks>
    void Watch(IReadOnlyList<string> directories);

    /// <summary>Stops watching. Safe to call when nothing is being watched.</summary>
    void Stop();
}
