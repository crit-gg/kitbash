namespace Workbench.Core.Platform;

/// <summary>
/// Actions whose implementation differs per operating system. Add members here rather
/// than testing the running OS at the call site.
/// </summary>
public interface IPlatformServices
{
    /// <summary>The running operating system.</summary>
    PlatformKind Kind { get; }

    /// <summary>Opens a web address in the default browser.</summary>
    void OpenInBrowser(WebAddress address);

    /// <summary>Opens a location in the default file browser.</summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    void OpenInFileBrowser(DirectoryLocation location);

    /// <summary>
    /// Starts a program that outlives Workbench, so closing the launcher leaves it running.
    /// </summary>
    /// <remarks>
    /// This is not the same as starting a program and not waiting for it.
    /// <see cref="IProcessRunner.Run"/> already does that, and a child started by it stays
    /// in Workbench's own process group, so anything aimed at the group reaches it too.
    /// Detaching is what takes it out of the group.
    /// </remarks>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    void StartDetached(ProcessRequest request);
}
