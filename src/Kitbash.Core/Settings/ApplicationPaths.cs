using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

/// <summary>
/// Where Kitbash keeps its own files for this user on this machine. These sit
/// outside any workspace, so they can be read before a workspace is known.
/// </summary>
public sealed class ApplicationPaths
{
    private const string ApplicationName = "Kitbash";
    private const string GlobalFileName = "kitbash.toml";
    private const string ToolStateFileName = "state.toml";
    private const string ToolsDirectoryName = "tools";
    private const string EnginesDirectoryName = "engines";
    private const string LayoutsDirectoryName = "layouts";

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

    /// <summary>
    /// Where Godot engines are installed when nothing says otherwise. Under the data
    /// directory rather than the cache, since an engine is a large download and losing it
    /// is not free.
    /// </summary>
    public string Engines => Path.Combine(State, EnginesDirectoryName);

    /// <summary>
    /// Where every installed tool lives, one folder per tool. Under the data directory
    /// beside the engines, since an install is a download and losing it is not free.
    /// </summary>
    public string Tools => Path.Combine(State, ToolsDirectoryName);

    /// <summary>
    /// Everything about one tool: its installed versions, a folder each, and the state
    /// file beside them. The folder need not exist.
    /// </summary>
    public string ToolDirectoryFor(string toolId) =>
        Path.Combine(Tools, SettingsScope.ForTool(toolId).ToolId!);

    /// <summary>The settings file backing one scope. The file need not exist.</summary>
    public string SettingsFileFor(SettingsScope scope) => FileIn(Configuration, scope);

    /// <summary>
    /// The state file backing one scope. A tool's sits inside its own folder, beside the
    /// versions installed for it, so one tool is one place. The file need not exist.
    /// </summary>
    public string StateFileFor(SettingsScope scope) =>
        scope.IsGlobal
            ? Path.Combine(State, GlobalFileName)
            : Path.Combine(ToolDirectoryFor(scope.ToolId!), ToolStateFileName);

    /// <summary>
    /// Where one view's window layout is kept. It is state rather than a setting, since
    /// the app writes it and it is right for one person on one machine.
    /// </summary>
    /// <param name="scope">Whose layout it is.</param>
    /// <param name="view">Which view inside that app, since an app has several.</param>
    public string LayoutFileFor(SettingsScope scope, string view)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);

        if (view.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{view}' cannot be used as a file name.", nameof(view));
        }

        var owner = scope.IsGlobal ? ApplicationName : scope.ToolId ?? ApplicationName;

        return Path.Combine(State, LayoutsDirectoryName, owner, view + ".json");
    }

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
