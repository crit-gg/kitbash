using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Godot;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>
/// One workspace being described. Every answer is held here until Create, so cancelling
/// leaves no folder, no project and no repository behind.
/// </summary>
public sealed partial class NewWorkspaceViewModel : ObservableObject
{
    private readonly IWorkspaceMaker _maker;
    private readonly IEngineStore _installs;
    private readonly IEngineCatalogue _catalogue;
    private readonly IGodotSettings _godot;

    /// <summary>
    /// The folder name the workspace name last wrote. The name only rewrites the last
    /// segment of the path while it is still this, so a folder typed by hand is left
    /// alone from then on. Godot's own <c>auto_dir</c>.
    /// </summary>
    private string _autoFolder = string.Empty;

    /// <summary>
    /// The folder name held while Create folder is off, so turning it back on restores the
    /// segment a person typed. Godot's own <c>last_custom_target_dir</c>.
    /// </summary>
    private string _lastCustomFolder = string.Empty;

    /// <summary>
    /// Which check is the current one. A check reads directories, so it runs off the UI
    /// thread and an answer that arrives after a newer one started is dropped.
    /// </summary>
    private int _checking;

    private GodotRenderer _renderer = GodotRenderer.ForwardPlus;

    [ObservableProperty]
    private string _name = "New workspace";

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _createsFolder = true;

    [ObservableProperty]
    private bool _hasGodotProject = true;

    /// <summary>Nothing, or Git. Two answers, so an index is enough to bind a list to.</summary>
    [ObservableProperty]
    private int _versionControl = 1;

    [ObservableProperty]
    private EngineChoiceViewModel? _engine;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>The status is a refusal, so the dialog cannot be accepted.</summary>
    [ObservableProperty]
    private bool _isBlocked = true;

    /// <summary>The engine list is still being read, so nothing can be picked yet.</summary>
    [ObservableProperty]
    private bool _isReadingEngines = true;

    public NewWorkspaceViewModel(
        IWorkspaceMaker maker,
        IEngineStore installs,
        IEngineCatalogue catalogue,
        IGodotSettings godot,
        string defaultDirectory)
    {
        ArgumentNullException.ThrowIfNull(maker);
        ArgumentNullException.ThrowIfNull(installs);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(defaultDirectory);

        _maker = maker;
        _installs = installs;
        _catalogue = catalogue;
        _godot = godot;

        Renderers =
        [
            new RendererChoiceViewModel(
                GodotRenderer.ForwardPlus,
                "Forward Plus",
                "Desktop only. The full lighting and post processing set, best on hardware "
                + "with a dedicated GPU.",
                Pick),
            new RendererChoiceViewModel(
                GodotRenderer.Mobile,
                "Mobile",
                "Runs on desktop and mobile. Fewer 3D features, much lighter on bandwidth "
                + "and memory.",
                Pick),
            new RendererChoiceViewModel(
                GodotRenderer.Compatibility,
                "Compatibility",
                "Runs on desktop, mobile and the web. The smallest feature set, and the "
                + "widest reach on old hardware.",
                Pick),
        ];

        // After the list exists, since choosing one clears the others.
        Pick(_renderer);

        _autoFolder = _maker.FolderNameFor(_name);

        // Blank is a real answer. It opens the dialog on the state that asks for a folder.
        if (defaultDirectory.Trim().Length > 0 && _autoFolder.Length > 0)
        {
            _path = System.IO.Path.Combine(defaultDirectory.Trim(), _autoFolder);
        }

        Recheck();
    }

    public ObservableCollection<EngineChoiceViewModel> Engines { get; } = [];

    public IReadOnlyList<RendererChoiceViewModel> Renderers { get; }

    /// <summary>
    /// A version that is not installed does not block Create, it only warns. Create
    /// installs it first, on the engines page where an install already reports itself.
    /// </summary>
    public bool NeedsInstall => HasGodotProject && Engine is { IsInstalled: false };

    public bool CanCreate => !IsBlocked && (!HasGodotProject || Engine is not null);

    /// <summary>Opening the project is the second half of Create, so the label says so.</summary>
    public string CreateLabel => HasGodotProject ? "Create and open" : "Create";

    public string GodotNote => HasGodotProject
        ? "A project.godot and its icon are written, and the workspace opens in the editor."
        : "The workspace is just a folder. Tools can write into it, and a project can be added at any time.";

    public string EngineNote => Engine is null
        ? "Reading what is installed and what Godot has published."
        : Engine.IsInstalled
            ? "Installed and ready. The workspace pins this version, so opening it later never picks a different engine."
            : "Not installed. Kitbash installs it before the project is created.";

    public string VersionControlNote => VersionControl == 0
        ? "No repository is created. You can add one later."
        : HasGodotProject
            ? "Initialises a repository and writes a .gitignore and .gitattributes for Godot."
            : "Initialises a repository in the workspace folder.";

    /// <summary>
    /// The last segment of the path while Create folder is on, which is the folder that
    /// will be made. Null with it off, so the field browses to the workspace folder itself.
    /// This is what ProjectDialog::_project_path_selected keeps across a browse.
    /// </summary>
    public string? FolderName => !CreatesFolder
        ? null
        : LastSegment(Path) is { Length: > 0 } segment ? segment : _autoFolder;

    /// <summary>What Create is handed. Read once, when the dialog has been accepted.</summary>
    public NewWorkspace Request => new()
    {
        Name = Name.Trim(),
        Path = Path.Trim(),
        CreatesFolder = CreatesFolder,
        HasGodotProject = HasGodotProject,
        Engine = HasGodotProject ? Engine?.Tag : null,
        Renderer = _renderer,
        UsesGit = VersionControl == 1,
    };

    /// <summary>
    /// Reads what is installed and what has been published, and opens the list on the
    /// default install. Touches a disk and a network, so nothing waits for it.
    /// </summary>
    public async Task LoadEnginesAsync()
    {
        IReadOnlyList<InstalledEngine> installed;
        IReadOnlyList<EngineRelease> releases;

        try
        {
            installed = await _installs.ReadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or EngineStoreException)
        {
            installed = [];
        }

        try
        {
            var read = await _catalogue.ReadReleasesAsync(false, CancellationToken.None).ConfigureAwait(true);
            releases = read.Releases;
        }
        catch (EngineCatalogueException)
        {
            // Nothing published is still a usable list, since an engine already on this
            // machine is the one a project is most likely to be made with.
            releases = [];
        }

        Fill(installed, releases);

        IsReadingEngines = false;
    }

    /// <summary>
    /// Installed first, newest release first, then everything else by release date. Both
    /// halves are rows of the same shape, and the right hand side says which half it is in.
    /// </summary>
    private void Fill(IReadOnlyList<InstalledEngine> installed, IReadOnlyList<EngineRelease> releases)
    {
        var here = installed
            .Where(engine => !engine.IsMissing)
            .Select(engine => engine.Tag)
            .Distinct()
            .OrderByDescending(tag => tag)
            .ToList();

        Engines.Clear();

        foreach (var tag in here)
        {
            Engines.Add(new EngineChoiceViewModel(tag, isInstalled: true));
        }

        foreach (var release in releases
            .Where(release => !here.Contains(release.Tag))
            .OrderByDescending(release => release.Released)
            .ThenByDescending(release => release.Tag))
        {
            Engines.Add(new EngineChoiceViewModel(release.Tag, isInstalled: false));
        }

        Engine = Opening();
    }

    /// <summary>
    /// The machine default, then the newest installed stable, then the newest stable
    /// published. A prerelease is never chosen for somebody.
    /// </summary>
    private EngineChoiceViewModel? Opening()
    {
        if (_godot.DefaultEngine is { } theDefault
            && Engines.FirstOrDefault(row => row.Tag == theDefault.Tag) is { } chosen)
        {
            return chosen;
        }

        return Engines.FirstOrDefault(row => row.IsInstalled && row.Tag.Channel == EngineChannel.Stable)
            ?? Engines.FirstOrDefault(row => row.Tag.Channel == EngineChannel.Stable)
            ?? Engines.FirstOrDefault();
    }

    private void Pick(GodotRenderer renderer)
    {
        _renderer = renderer;

        foreach (var choice in Renderers)
        {
            choice.IsChosen = choice.Renderer == renderer;
        }
    }

    // The name writes the last segment of the path while that segment is still the one it
    // wrote last time. This is ProjectDialog::_update_target_auto_dir.
    partial void OnNameChanged(string value)
    {
        var next = _maker.FolderNameFor(value);

        if (CreatesFolder
            && next.Length > 0
            && System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path.Trim())) == _autoFolder)
        {
            Path = Above(Path) is { Length: > 0 } parent
                ? System.IO.Path.Combine(parent, next)
                : next;
        }

        _autoFolder = next;

        OnPropertyChanged(nameof(FolderName));
        Recheck();
    }

    partial void OnPathChanged(string value)
    {
        OnPropertyChanged(nameof(FolderName));
        Recheck();
    }

    // Turning it on appends a folder name and turning it off takes one away, so the field
    // always holds the folder the workspace will be. ProjectDialog::_create_dir_toggled.
    partial void OnCreatesFolderChanged(bool value)
    {
        var target = System.IO.Path.TrimEndingDirectorySeparator(Path.Trim());

        if (value)
        {
            var folder = _lastCustomFolder.Length > 0 ? _lastCustomFolder : _autoFolder;

            Path = target.Length > 0 && folder.Length > 0
                ? System.IO.Path.Combine(target, folder)
                : Path;
        }
        else if (target.Length > 0 && Above(target) is { Length: > 0 } parent)
        {
            var folder = System.IO.Path.GetFileName(target);

            _lastCustomFolder = folder == _autoFolder ? string.Empty : folder;
            Path = parent;
        }

        OnPropertyChanged(nameof(FolderName));
        Recheck();
    }

    partial void OnHasGodotProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(CreateLabel));
        OnPropertyChanged(nameof(GodotNote));
        OnPropertyChanged(nameof(VersionControlNote));
        OnPropertyChanged(nameof(NeedsInstall));
        OnPropertyChanged(nameof(CanCreate));

        Recheck();
    }

    partial void OnVersionControlChanged(int value) => OnPropertyChanged(nameof(VersionControlNote));

    partial void OnEngineChanged(EngineChoiceViewModel? value)
    {
        OnPropertyChanged(nameof(EngineNote));
        OnPropertyChanged(nameof(NeedsInstall));
        OnPropertyChanged(nameof(CanCreate));
    }

    partial void OnIsBlockedChanged(bool value) => OnPropertyChanged(nameof(CanCreate));

    /// <summary>
    /// Runs the check away from the UI thread and puts the answer on screen when it lands.
    /// The status line is always drawn, so this only ever changes its wording and its tone.
    /// </summary>
    private async void Recheck()
    {
        var request = Request;
        var generation = ++_checking;

        var state = await Task.Run(() => _maker.Check(request)).ConfigureAwait(true);

        if (generation != _checking)
        {
            return;
        }

        Status = Explain(state);
        IsBlocked = state is not (NewWorkspaceState.Ready or NewWorkspaceState.WillCreate);
    }

    private string Explain(NewWorkspaceState state) => state switch
    {
        NewWorkspaceState.NameMissing => "Give the workspace a name",
        NewWorkspaceState.PathMissing => "Pick a folder for the workspace",
        NewWorkspaceState.PathNotFull => "That is not a full path",
        NewWorkspaceState.FolderNameNotAllowed => "The folder name uses characters that are not allowed",
        NewWorkspaceState.WorkspaceExists => "A workspace already lives in that folder",
        NewWorkspaceState.ParentMissing => "The folder above that one does not exist",
        NewWorkspaceState.FolderMissing => "That folder does not exist",
        NewWorkspaceState.FolderNotEmpty => "That folder is not empty",
        NewWorkspaceState.WillCreate => HasGodotProject
            ? "The folder will be created and the project set up inside it"
            : "The folder will be created and left empty",
        _ => "The folder is empty and ready",
    };

    /// <summary>The last part of a path, or empty when it has none.</summary>
    private static string LastSegment(string path) =>
        System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path.Trim()));

    /// <summary>The folder above a path, or empty when there is none.</summary>
    private static string Above(string path)
    {
        var trimmed = System.IO.Path.TrimEndingDirectorySeparator(path.Trim());

        return trimmed.Length == 0 ? string.Empty : System.IO.Path.GetDirectoryName(trimmed) ?? string.Empty;
    }
}
