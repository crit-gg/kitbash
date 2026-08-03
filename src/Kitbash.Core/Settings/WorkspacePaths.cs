namespace Kitbash.Core.Settings;

/// <summary>
/// Where a workspace keeps its settings files. A workspace is any directory holding
/// a <c>.kitbash</c> folder. Use <see cref="IWorkspaceLocator"/> to find one.
/// </summary>
public sealed class WorkspacePaths
{
    public const string KitbashDirectoryName = ".kitbash";

    private const string GlobalFileName = "kitbash.toml";
    private const string ToolsDirectoryName = "tools";

    public WorkspacePaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The directory holding the <c>.kitbash</c> folder.</summary>
    public string Root { get; }

    public string KitbashDirectory => Path.Combine(Root, KitbashDirectoryName);

    /// <summary>Team settings, committed to git.</summary>
    public string TeamSharedDirectory => Path.Combine(KitbashDirectory, "config");

    /// <summary>Personal settings, kept out of git.</summary>
    public string UserDirectory => Path.Combine(KitbashDirectory, "user");

    /// <summary>Path backing one scope in one layer. The file need not exist.</summary>
    public string FileFor(SettingsScope scope, SettingsLayer layer)
    {
        var layerDirectory = layer switch
        {
            SettingsLayer.TeamShared => TeamSharedDirectory,
            SettingsLayer.User => UserDirectory,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown settings layer."),
        };

        if (scope.IsGlobal)
        {
            return Path.Combine(layerDirectory, GlobalFileName);
        }

        return Path.Combine(layerDirectory, ToolsDirectoryName, scope.ToolId + ".toml");
    }
}
