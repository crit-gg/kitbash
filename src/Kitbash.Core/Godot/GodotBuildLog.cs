using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

internal sealed class GodotBuildLog : IGodotBuildLog
{
    private const string LogFileName = "msbuild_log.txt";

    /// <summary>Room for a filesystem that stores a write time more coarsely than the clock reads it.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(2);

    private readonly IEngineFiles _engineFiles;
    private readonly IFileSystem _fileSystem;

    public GodotBuildLog(IEngineFiles engineFiles, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _engineFiles = engineFiles;
        _fileSystem = fileSystem;
    }

    public string? Read(InstalledEngine engine, GodotProject project, DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(project);

        var root = Path.Combine(_engineFiles.DataDirectoryFor(engine.Executable), "mono", "build_logs");

        // Godot names each folder after a hash of the solution path, so a log is matched by
        // the project files it names. Godot writes those with forward slashes on Windows.
        var directory = Slashed(Path.TrimEndingDirectorySeparator(project.Directory)) + "/";

        string? newest = null;
        DateTimeOffset newestAt = default;

        try
        {
            if (!_fileSystem.DirectoryExists(root))
            {
                return null;
            }

            foreach (var folder in _fileSystem.EnumerateDirectories(root))
            {
                var file = Path.Combine(folder, LogFileName);

                if (_fileSystem.GetLastWriteTime(file) is not { } at || at < since - Slack
                    || (newest is not null && at <= newestAt))
                {
                    continue;
                }

                var text = _fileSystem.ReadAllText(file);

                if (Slashed(text).Contains(directory, StringComparison.OrdinalIgnoreCase))
                {
                    newest = text;
                    newestAt = at;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return newest;
        }

        return newest;
    }

    private static string Slashed(string text) => text.Replace('\\', '/');
}
