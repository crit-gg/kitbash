using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

public sealed class WorkspaceLocator : IWorkspaceLocator
{
    private readonly IFileSystem _fileSystem;

    public WorkspaceLocator(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public WorkspacePaths? Discover(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = Path.GetFullPath(startDirectory);

        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, WorkspacePaths.KitbashDirectoryName);

            if (_fileSystem.DirectoryExists(candidate))
            {
                return new WorkspacePaths(directory);
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
