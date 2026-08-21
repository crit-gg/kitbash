using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Workspaces;

internal sealed class WorkspaceNameResolver : IWorkspaceNameResolver
{
    /// <summary>The key a team can set to name the workspace themselves.</summary>
    public const string NameKey = "workspace.name";

    private readonly IFileSystem _fileSystem;
    private readonly ISettingsDocumentStore _store;
    private readonly IGodotProjectReader _projects;
    private readonly IWorkspaceScaffold _scaffold;

    public WorkspaceNameResolver(
        IFileSystem fileSystem,
        ISettingsDocumentStore store,
        IGodotProjectReader projects,
        IWorkspaceScaffold scaffold)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(scaffold);

        _fileSystem = fileSystem;
        _store = store;
        _projects = projects;
        _scaffold = scaffold;
    }

    public string Resolve(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return ConfiguredName(root, SettingsLayer.User)
            ?? ConfiguredName(root, SettingsLayer.TeamShared)
            ?? GodotProjectName(root)
            ?? FolderName(root);
    }

    /// <summary>
    /// The user layer, so renaming from the launcher never shows up as a change to a
    /// committed file. The team layer still names it for everybody who has no name of
    /// their own.
    /// </summary>
    public void SetName(string root, string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var trimmed = name?.Trim() ?? string.Empty;
        var path = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.User);

        _store.Apply(
            path,
            trimmed.Length == 0
                ? [SettingsEdit.Remove(NameKey)]
                : [SettingsEdit.Set(NameKey, trimmed)]);

        _scaffold.EnsureUserLayerIgnored(root);
    }

    private string? ConfiguredName(string root, SettingsLayer layer)
    {
        var path = new WorkspacePaths(root).FileFor(SettingsScope.Global, layer);

        if (!_fileSystem.FileExists(path))
        {
            return null;
        }

        // This runs while the launcher is starting, so a file that will not parse gives
        // up its name rather than the whole start.
        var document = _store.Open(path).Document;

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
