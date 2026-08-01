using Workbench.Core.IO;

namespace Workbench.Core.Settings;

/// <summary>
/// Where Workbench keeps its own files for this user on this machine. These sit
/// outside any workspace, so they can be read before a workspace is known.
/// </summary>
public sealed class ApplicationPaths
{
    private const string ApplicationName = "Workbench";
    private const string GlobalFileName = "workbench.toml";
    private const string ToolsDirectoryName = "tools";

    public ApplicationPaths(IUserDirectories directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        Configuration = directories.ConfigurationFor(ApplicationName);
        State = directories.StateFor(ApplicationName);
        Cache = directories.CacheFor(ApplicationName);
    }

    /// <summary>Settings a person may edit.</summary>
    public string Configuration { get; }

    /// <summary>What the app remembers. Deleting it loses choices, never work.</summary>
    public string State { get; }

    /// <summary>Files that can be built again. Deleting it costs only the rebuild.</summary>
    public string Cache { get; }

    /// <summary>The settings file backing one scope. The file need not exist.</summary>
    public string SettingsFileFor(SettingsScope scope) => FileIn(Configuration, scope);

    /// <summary>The state file backing one scope. The file need not exist.</summary>
    public string StateFileFor(SettingsScope scope) => FileIn(State, scope);

    /// <summary>A named place in the cache. Nothing is created until something writes.</summary>
    public string CacheFileFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{name}' cannot be used as a file name.", nameof(name));
        }

        return Path.Combine(Cache, name);
    }

    private static string FileIn(string root, SettingsScope scope) =>
        scope.IsGlobal
            ? Path.Combine(root, GlobalFileName)
            : Path.Combine(root, ToolsDirectoryName, scope.ToolId + ".toml");
}
