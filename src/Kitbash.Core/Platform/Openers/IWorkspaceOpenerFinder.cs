namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// What is installed on this machine. One implementation per operating system, chosen by
/// the factory in KitbashCoreServices.
/// </summary>
public interface IWorkspaceOpenerFinder
{
    /// <summary>
    /// Everything found, in the order it should be offered. Touches a disk and runs a
    /// program, so never call it on the UI thread.
    /// </summary>
    Task<IReadOnlyList<WorkspaceOpener>> FindAsync(CancellationToken cancellation = default);
}
