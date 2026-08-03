using Workbench.Core.IO;

namespace Workbench.Core.Platform;

public sealed class ExecutableFinder : IExecutableFinder
{
    /// <summary>
    /// What Windows falls back to when PATHEXT is unset. Every Windows install sets it,
    /// so this is the answer to a variable that is missing rather than a list to maintain.
    /// </summary>
    private const string WindowsFallbackExtensions = ".COM;.EXE;.BAT;.CMD";

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly IBundleEnvironment _bundle;

    public ExecutableFinder(IFileSystem fileSystem, IEnvironment environment, IBundleEnvironment bundle)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(bundle);

        _fileSystem = fileSystem;
        _environment = environment;
        _bundle = bundle;
    }

    public string? Find(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // The same PATH a child would get, so what is found here is what runs. Inside an
        // AppImage the inherited one leads with the bundle's own directory.
        var path = _bundle.Outside.TryGetValue("PATH", out var corrected)
            ? corrected
            : _environment.GetVariable("PATH");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var names = Names(fileName);

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(directory, name);

                if (_fileSystem.IsExecutableFile(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The names to try for one program.
    /// </summary>
    private IEnumerable<string> Names(string fileName)
    {
        yield return fileName;

        if (!OperatingSystem.IsWindows() || Path.HasExtension(fileName))
        {
            yield break;
        }

        var extensions = _environment.GetVariable("PATHEXT");

        if (string.IsNullOrWhiteSpace(extensions))
        {
            extensions = WindowsFallbackExtensions;
        }

        foreach (var extension in extensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return fileName + extension.Trim();
        }
    }
}
