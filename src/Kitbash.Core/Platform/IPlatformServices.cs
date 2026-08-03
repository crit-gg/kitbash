namespace Kitbash.Core.Platform;

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
    /// Starts a program that outlives Kitbash, so closing the launcher leaves it running.
    /// </summary>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    void StartDetached(ProcessRequest request);
}
