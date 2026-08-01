using Workbench.Core.IO;

namespace Workbench.Core.Platform;

public sealed class ExecutableFinder : IExecutableFinder
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;

    public ExecutableFinder(IFileSystem fileSystem, IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);

        _fileSystem = fileSystem;
        _environment = environment;
    }

    public string? Find(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var path = _environment.GetVariable("PATH");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);

            if (_fileSystem.IsExecutableFile(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
