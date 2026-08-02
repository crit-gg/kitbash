using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Settings;

namespace Workbench.Core.Workspaces;

internal sealed class WorkspaceNameResolver : IWorkspaceNameResolver
{
    /// <summary>The key a team can set to name the workspace themselves.</summary>
    public const string NameKey = "workspace.name";

    private readonly IFileSystem _fileSystem;
    private readonly ISettingsDocumentStore _store;
    private readonly IGodotProjectReader _projects;

    public WorkspaceNameResolver(
        IFileSystem fileSystem,
        ISettingsDocumentStore store,
        IGodotProjectReader projects)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(projects);

        _fileSystem = fileSystem;
        _store = store;
        _projects = projects;
    }

    public string Resolve(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return ConfiguredName(root)
            ?? GodotProjectName(root)
            ?? FolderName(root);
    }

    private string? ConfiguredName(string root)
    {
        var path = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        if (!_fileSystem.FileExists(path))
        {
            return null;
        }

        var document = _store.Read(path);

        return document.TryGetValue(NameKey, out var value) && value is string name && name.Length > 0
            ? name
            : null;
    }

    // The search and the parse belong to the project reader, which the engine strip needs
    // as well, so this asks for the project rather than looking for the file itself.
    private string? GodotProjectName(string root) =>
        _projects.Find(root) is { Name.Length: > 0 } project ? project.Name : null;

    private static string FolderName(string root) =>
        Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}
