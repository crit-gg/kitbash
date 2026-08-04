using System.Text;
using Kitbash.Core.Git;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Core.Workspaces;

/// <summary>
/// The rules and the writes behind Create new workspace. The checks follow
/// ProjectDialog::_validate_path in the Godot 4 editor, except that a folder with files
/// in it is an error here and a warning there.
/// </summary>
internal sealed class WorkspaceMaker : IWorkspaceMaker
{
    /// <summary>
    /// The characters OS::get_safe_dir_name replaces. The set is Godot's on every
    /// platform, so a folder made here opens on the other one too.
    /// </summary>
    private static readonly char[] Unsafe = [':', '*', '?', '"', '<', '>', '|', '/', '\\'];

    private readonly IFileSystem _fileSystem;
    private readonly IPathRules _paths;
    private readonly IWorkspaceRegistry _workspaces;
    private readonly IWorkspaceScaffold _scaffold;
    private readonly IGodotProjectWriter _projects;
    private readonly IGitInitializer _git;
    private readonly ISettingsDocumentStore _settings;
    private readonly WorkspaceGodotSettingsSchema _godot;

    public WorkspaceMaker(
        IFileSystem fileSystem,
        IPathRules paths,
        IWorkspaceRegistry workspaces,
        IWorkspaceScaffold scaffold,
        IGodotProjectWriter projects,
        IGitInitializer git,
        ISettingsDocumentStore settings,
        WorkspaceGodotSettingsSchema godot)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(scaffold);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(godot);

        _fileSystem = fileSystem;
        _paths = paths;
        _workspaces = workspaces;
        _scaffold = scaffold;
        _projects = projects;
        _git = git;
        _settings = settings;
        _godot = godot;
    }

    public NewWorkspaceState Check(NewWorkspace request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name.Trim().Length == 0)
        {
            return NewWorkspaceState.NameMissing;
        }

        if (request.Path.Trim().Length == 0)
        {
            return NewWorkspaceState.PathMissing;
        }

        if (Resolve(request.Path) is not { Length: > 0 } target)
        {
            return NewWorkspaceState.PathNotFull;
        }

        var folder = Path.GetFileName(target);

        // A root such as C:\ or / has no last segment, so there is no folder to make and
        // no folder to name the workspace after.
        if (folder.Length == 0 || !string.Equals(folder, FolderNameFor(folder), StringComparison.Ordinal))
        {
            return NewWorkspaceState.FolderNameNotAllowed;
        }

        if (_workspaces.All.Any(workspace =>
                _paths.AreSame(workspace.Root, target) || _paths.Contains(workspace.Root, target)))
        {
            return NewWorkspaceState.WorkspaceExists;
        }

        if (request.CreatesFolder)
        {
            if (Path.GetDirectoryName(target) is not { Length: > 0 } parent
                || !_fileSystem.DirectoryExists(parent))
            {
                return NewWorkspaceState.ParentMissing;
            }

            if (!_fileSystem.DirectoryExists(target))
            {
                return NewWorkspaceState.WillCreate;
            }
        }
        else if (!_fileSystem.DirectoryExists(target))
        {
            return NewWorkspaceState.FolderMissing;
        }

        return IsEmpty(target) ? NewWorkspaceState.Ready : NewWorkspaceState.FolderNotEmpty;
    }

    public string Resolve(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = path.Trim();

        if (trimmed.Length == 0 || !Path.IsPathFullyQualified(trimmed))
        {
            return string.Empty;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    public string FolderNameFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var text = new StringBuilder(name.Trim());

        // Godot's own two replacements for the names a filesystem reserves.
        if (text.ToString() is "." or "..")
        {
            return text.ToString() == "." ? "dot" : "twodots";
        }

        for (var at = 0; at < text.Length; at++)
        {
            if (Array.IndexOf(Unsafe, text[at]) >= 0)
            {
                text[at] = '-';
            }
        }

        // Windows will not take a folder name ending in a period, and Godot trims it on
        // every platform so the answer is the same on both.
        return text.ToString().TrimEnd('.');
    }

    public async Task<Workspace> MakeAsync(NewWorkspace request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = Resolve(request.Path);

        if (target.Length == 0)
        {
            throw new IOException($"'{request.Path}' is not a path a workspace can be made at.");
        }

        var name = request.Name.Trim();

        // Does nothing when the folder is already there, which is the Create folder off case.
        _fileSystem.CreateDirectory(target);

        if (request.HasGodotProject && request.Engine is { } engine)
        {
            _projects.Write(new NewGodotProject(target, name, engine, request.Renderer));

            if (request.UsesGit)
            {
                _projects.WriteGitFiles(target);
            }
        }

        // The .kitbash directory, the entry in the list, and the team config the scaffold
        // writes behind it. Everything below writes into that config, so it goes first.
        var workspace = _workspaces.Add(target);

        Describe(target, name, request);

        if (request.UsesGit)
        {
            await _git.InitialiseAsync(target, cancellation).ConfigureAwait(false);

            // The personal layer sits inside .kitbash, so it carries its own rule rather
            // than waiting for somebody to write one into the repository.
            _scaffold.EnsureUserLayerIgnored(target);
        }

        _workspaces.SetCurrent(workspace.Root);
        _workspaces.Refresh();

        return _workspaces.All.FirstOrDefault(known => _paths.AreSame(known.Root, workspace.Root)) ?? workspace;
    }

    /// <summary>
    /// Writes the name and the engine pin into the workspace's team config, which is the
    /// shared layer and not the one renaming writes.
    /// </summary>
    private void Describe(string target, string name, NewWorkspace request)
    {
        var edits = new List<SettingsEdit> { SettingsEdit.Set(WorkspaceNameResolver.NameKey, name) };

        if (request.HasGodotProject && request.Engine is { } engine)
        {
            edits.Add(SettingsEdit.Set(_godot.Engine.Key, engine.ToString()));
        }

        try
        {
            _settings.Apply(new WorkspacePaths(target).FileFor(SettingsScope.Global, SettingsLayer.TeamShared), edits);
        }
        catch (Exception exception) when (exception is SettingsFileUnreadableException
            or IOException or UnauthorizedAccessException)
        {
            // The workspace is made and the folder name still names it. A config that will
            // not take these is not a reason to throw the whole thing away.
        }
    }

    /// <summary>
    /// Whether anything is in the folder. A dot file does not count, which is Godot's own
    /// rule and is what lets a repository be initialised before a project is made in it.
    /// </summary>
    private bool IsEmpty(string directory) =>
        !_fileSystem.EnumerateDirectories(directory).Concat(_fileSystem.EnumerateFiles(directory, recursive: false))
            .Select(Path.GetFileName)
            .Any(entry => entry is { Length: > 0 } && !entry.StartsWith('.'));
}
