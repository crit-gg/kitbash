using System.Text.RegularExpressions;
using Workbench.Core.IO;
using Workbench.Core.Settings;

namespace Workbench.Core.Workspaces;

internal sealed partial class WorkspaceNameResolver : IWorkspaceNameResolver
{
    /// <summary>The key a team can set to name the workspace themselves.</summary>
    public const string NameKey = "workspace.name";

    private const string GodotProjectFile = "project.godot";

    /// <summary>How deep to look for a Godot project before giving up.</summary>
    private const int MaxDepth = 4;

    private static readonly string[] SkippedDirectories =
        [".git", ".godot", ".import", ".workbench", "node_modules", "bin", "obj"];

    private readonly IFileSystem _fileSystem;
    private readonly ISettingsDocumentStore _store;

    public WorkspaceNameResolver(IFileSystem fileSystem, ISettingsDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(store);

        _fileSystem = fileSystem;
        _store = store;
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

    private string? GodotProjectName(string root)
    {
        if (FindGodotProject(root, depth: 0) is not { } projectFile)
        {
            return null;
        }

        var match = ConfigName().Match(_fileSystem.ReadAllText(projectFile));

        return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
    }

    private string? FindGodotProject(string directory, int depth)
    {
        var candidate = Path.Combine(directory, GodotProjectFile);

        if (_fileSystem.FileExists(candidate))
        {
            return candidate;
        }

        if (depth >= MaxDepth)
        {
            return null;
        }

        foreach (var child in _fileSystem.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);

            if (name.StartsWith('.') || SkippedDirectories.Contains(name))
            {
                continue;
            }

            if (FindGodotProject(child, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string FolderName(string root) =>
        Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    // project.godot is not TOML. Its keys contain slashes, so it is read by line.
    [GeneratedRegex("""^\s*config/name\s*=\s*"(.*)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex ConfigName();
}
