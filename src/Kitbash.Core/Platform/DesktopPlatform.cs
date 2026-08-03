using Kitbash.Core.IO;

namespace Kitbash.Core.Platform;

/// <summary>
/// Shared behavior for every desktop. Subclasses supply <see cref="Open"/> and
/// <see cref="Detach"/>, which are the two things that differ per operating system.
/// </summary>
internal abstract class DesktopPlatform : IPlatformServices
{
    private readonly IFileSystem _fileSystem;

    protected DesktopPlatform(IFileSystem fileSystem, IProcessRunner processes)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(processes);

        _fileSystem = fileSystem;
        Processes = processes;
    }

    public abstract PlatformKind Kind { get; }

    protected IProcessRunner Processes { get; }

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

    public void StartDetached(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Processes.Run(Detach(request));
    }

    /// <summary>Hands an already checked target to the desktop.</summary>
    protected abstract void Open(string target);

    /// <summary>
    /// The same request, written the way this operating system starts something that
    /// outlives its parent. Returning the request unchanged is a valid answer.
    /// </summary>
    protected abstract ProcessRequest Detach(ProcessRequest request);

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
