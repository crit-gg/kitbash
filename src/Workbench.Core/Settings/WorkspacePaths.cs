namespace Workbench.Core.Settings;

/// <summary>
/// Where a workspace keeps its settings files. A workspace is any directory holding
/// a <c>.workbench</c> folder.
/// </summary>
public sealed class WorkspacePaths
{
    public const string WorkbenchDirectoryName = ".workbench";

    private const string GlobalFileName = "workbench.toml";
    private const string ToolsDirectoryName = "tools";

    public WorkspacePaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The directory holding the <c>.workbench</c> folder.</summary>
    public string Root { get; }

    public string WorkbenchDirectory => Path.Combine(Root, WorkbenchDirectoryName);

    /// <summary>Team settings, committed to git.</summary>
    public string TeamSharedDirectory => Path.Combine(WorkbenchDirectory, "config");

    /// <summary>Personal settings, kept out of git.</summary>
    public string UserDirectory => Path.Combine(WorkbenchDirectory, "user");

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a workspace, the
    /// way git finds <c>.git</c>. Returns null when there is none, which is normal.
    /// </summary>
    public static WorkspacePaths? Discover(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, WorkbenchDirectoryName)))
            {
                return new WorkspacePaths(directory.FullName);
            }

            directory = directory.Parent;
        }

        return null;
    }

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
