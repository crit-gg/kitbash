using Workbench.Core.IO;
using Workbench.Core.Settings;

namespace Workbench.Core.Workspaces;

internal sealed class WorkspaceRegistry : IWorkspaceRegistry
{
    private const string KnownKey = "workspaces.known";
    private const string CurrentKey = "workspaces.current";

    private readonly IApplicationState _state;
    private readonly IWorkspaceNameResolver _names;
    private readonly IWorkspaceScaffold _scaffold;
    private readonly IFileSystem _fileSystem;
    private readonly IPathRules _paths;
    private readonly Lock _gate = new();

    private List<Workspace> _all = [];
    private string? _currentRoot;

    public WorkspaceRegistry(
        IApplicationState state,
        IWorkspaceNameResolver names,
        IWorkspaceScaffold scaffold,
        IFileSystem fileSystem,
        IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(scaffold);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);

        _state = state;
        _names = names;
        _scaffold = scaffold;
        _fileSystem = fileSystem;
        _paths = paths;

        Refresh();
    }

    public IReadOnlyList<Workspace> All
    {
        get
        {
            lock (_gate)
            {
                return _all;
            }
        }
    }

    public Workspace? Current
    {
        get
        {
            lock (_gate)
            {
                return _all.FirstOrDefault(workspace => IsSameRoot(workspace.Root, _currentRoot));
            }
        }
    }

    public Workspace Add(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var root = Path.GetFullPath(folder);

        if (!_fileSystem.DirectoryExists(root))
        {
            throw new DirectoryNotFoundException($"'{root}' does not exist.");
        }

        lock (_gate)
        {
            var known = ReadKnown();

            // Refused before anything is written, so a rejected folder does not get a
            // .workbench directory left behind in it.
            if (known.FirstOrDefault(existing => _paths.Contains(existing, root)) is { } containing)
            {
                throw new NestedWorkspaceException(root, containing);
            }

            // Any folder can be a workspace. What makes it one is the .workbench directory.
            _fileSystem.CreateDirectory(Path.Combine(root, WorkspacePaths.WorkbenchDirectoryName));

            if (!known.Any(existing => IsSameRoot(existing, root)))
            {
                known.Add(root);
                _state.Set(SettingsScope.Global, KnownKey, known.ToArray());
            }

            if (_currentRoot is null)
            {
                SetCurrentCore(root);
            }
        }

        Refresh();

        return All.First(workspace => IsSameRoot(workspace.Root, root));
    }

    public void Remove(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        lock (_gate)
        {
            var known = ReadKnown();
            known.RemoveAll(existing => IsSameRoot(existing, root));
            _state.Set(SettingsScope.Global, KnownKey, known.ToArray());

            if (IsSameRoot(_currentRoot, root))
            {
                SetCurrentCore(known.FirstOrDefault());
            }
        }

        Refresh();
    }

    public void SetCurrent(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        lock (_gate)
        {
            SetCurrentCore(Path.GetFullPath(root));
        }

        Refresh();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            _state.Reload();

            _all = [.. ReadKnown().Select(Resolve)];
            _currentRoot = _state.Global.Get<string?>(CurrentKey, null);
        }
    }

    private Workspace Resolve(string root)
    {
        var exists = _fileSystem.DirectoryExists(root);

        if (!exists)
        {
            // A missing folder keeps the last thing we can still say about it.
            return new Workspace(root, Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), false, false);
        }

        // Every workspace gets the config file, not only the ones added from here. A
        // folder cloned with a .workbench already in it, or one added before this
        // existed, is otherwise a workspace with nothing to read or edit.
        _scaffold.Ensure(root);

        return new Workspace(root, _names.Resolve(root), true, HasRepository(root));
    }

    // A worktree or submodule carries .git as a file rather than a directory.
    private bool HasRepository(string root)
    {
        var git = Path.Combine(root, ".git");

        return _fileSystem.DirectoryExists(git) || _fileSystem.FileExists(git);
    }

    // A blank entry would be a path nothing can be asked about, so it is dropped.
    private List<string> ReadKnown() =>
    [
        .. _state.Global
            .Get(KnownKey, Array.Empty<string>())
            .Where(root => !string.IsNullOrWhiteSpace(root)),
    ];

    private void SetCurrentCore(string? root)
    {
        _currentRoot = root;
        _state.Set(SettingsScope.Global, CurrentKey, root ?? string.Empty);
    }

    // Blanks reach here because no current workspace is stored as an empty string.
    private bool IsSameRoot(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && _paths.AreSame(left, right);
}
