using Workbench.Core.IO;

namespace Workbench.Core.Platform;

/// <summary>
/// Shared behavior for every desktop. Subclasses supply only <see cref="Open"/>,
/// which is the single thing that differs per operating system.
/// </summary>
internal abstract class DesktopPlatform : IPlatformServices
{
    private readonly IFileSystem _fileSystem;

    protected DesktopPlatform(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public abstract PlatformKind Kind { get; }

    public void OpenInBrowser(WebAddress address) => Launch(address.ToString());

    public void OpenInFileBrowser(DirectoryLocation location)
    {
        var path = location.Value;

        // A file would be run rather than shown, so refuse before launching anything.
        if (_fileSystem.FileExists(path))
        {
            throw new ArgumentException(
                $"'{path}' is a file. Pass the directory that holds it.",
                nameof(location));
        }

        if (!_fileSystem.DirectoryExists(path))
        {
            throw new DirectoryNotFoundException($"'{path}' does not exist.");
        }

        Launch(path);
    }

    /// <summary>Hands an already checked target to the desktop.</summary>
    protected abstract void Open(string target);

    private void Launch(string target)
    {
        try
        {
            Open(target);
        }
        catch (ProcessStartException exception)
        {
            throw new InvalidOperationException($"Could not open '{target}'.", exception);
        }
    }
}
