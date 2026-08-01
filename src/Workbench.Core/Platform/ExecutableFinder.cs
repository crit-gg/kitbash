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
    /// <remarks>
    /// On Unix a program is its name and the executable bit says the rest. On Windows the
    /// name on PATH is <c>git.exe</c> while every caller asks for <c>git</c>, so the
    /// extensions in PATHEXT are tried in the order that variable gives, which is the
    /// order the shell itself uses. A caller that already named an extension gets that
    /// name alone.
    /// </remarks>
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
