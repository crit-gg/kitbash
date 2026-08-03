using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core;
using Workbench.Core.Git;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;
using Workbench.Core.Workspaces;

namespace Workbench.ViewModels;

public partial class LauncherViewModel : ViewModelBase, IDisposable
{
    /// <summary>Roughly what fits the selector and a switcher row at their fixed widths.</summary>
    private const int SubtitleLength = 34;
    private const int RowPathLength = 48;

    private readonly IWorkspaceRegistry _workspaces;
    private readonly IPathShortener _paths;
    private readonly IGitStatusMonitor _git;
    private readonly IGitUpdater _updater;
    private readonly IUiDispatcher _dispatcher;
    private readonly IEngineRequirementReader _requirements;
    private readonly IEngineStore _engines;
    private readonly IEngineResolver _resolver;
    private readonly IGodotSettings _godot;
    private readonly IGodotLauncher _godotLauncher;
    private readonly IPlatformServices _platform;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private readonly SemaphoreSlim _reading = new(1, 1);

    [ObservableProperty]
    private bool _workspacesOpen;

    [ObservableProperty]
    private string _workspaceName = "No workspace";

    [ObservableProperty]
    private string _workspaceSubtitle = "Add one to get started";

    /// <summary>False shows the empty state instead of the whole page.</summary>
    [ObservableProperty]
    private bool _hasWorkspaces;

    /// <summary>
    /// Which rail item is open, as its index. The rail is a ListBox, so selection,
    /// keyboard navigation and the open state are all its own and this only says which.
    /// </summary>
    [ObservableProperty]
    private int _page;

    /// <summary>
    /// The engine strip. Replaced whole by <see cref="RefreshEngineAsync"/> rather than
    /// written into, so it is never half a state.
    /// </summary>
    [ObservableProperty]
    private EngineViewModel _engine = EngineViewModel.Reading;

    public LauncherViewModel(
        IWorkspaceRegistry workspaces,
        IPathShortener paths,
        IToolRegistry tools,
        IGitStatusMonitor git,
        IGitUpdater updater,
        IUiDispatcher dispatcher,
        IEngineRequirementReader requirements,
        IEngineStore engineStore,
        IEngineResolver resolver,
        IGodotSettings godot,
        IGodotLauncher godotLauncher,
        IPlatformServices platform,
        EnginesViewModel engines)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(engineStore);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(godotLauncher);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(engines);

        _workspaces = workspaces;
        _paths = paths;
        _git = git;
        _updater = updater;
        _dispatcher = dispatcher;
        _requirements = requirements;
        _engines = engineStore;
        _resolver = resolver;
        _godot = godot;
        _godotLauncher = godotLauncher;
        _platform = platform;

        Engines = engines;

        Tools = [.. tools.Tools.Select(tool => new ToolCardViewModel(tool))];

        // The monitor reads on its own threads, so what it says has to be carried over
        // before anything bound to it is touched.
        _git.Changed += OnGitChanged;

        // The strip describes what is installed, so it follows the page that changes it.
        // Installing the version a workspace asks for has to turn the strip green without
        // anybody switching workspaces to make it notice.
        Engines.InstallsChanged += OnInstallsChanged;

        // The one read that stays here. This runs before the window exists, so there is no
        // frame to drop and nothing to feel, and doing it now means the launcher opens
        // filled in rather than opening empty and filling in a moment later.
        Apply(Read());

        // Deliberately not part of that read. The read above runs before the window exists
        // and has to stay quick, and this one asks the disk about every install and can
        // run a process. The strip says it is reading until this comes back.
        _ = RefreshEngineAsync();
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    /// <summary>The engines page, which the rail's second item opens.</summary>
    public EnginesViewModel Engines { get; }

    public bool OnWorkspacePage => Page == 0;

    public bool OnEnginesPage => Page == 1;

    /// <summary>
    /// The repository the open workspace sits in, kept current by the monitor. Empty of a
    /// status when the workspace is not a repository, and the plain footer shows instead.
    /// </summary>
    public GitViewModel Git { get; } = new();

    /// <summary>
    /// The registered tools. This is the registry's list rather than a written one, so
    /// the launcher shows what the app actually offers.
    /// </summary>
    public IReadOnlyList<ToolCardViewModel> Tools { get; }

    /// <summary>
    /// Registers a folder and opens it. A folder inside a workspace already added is
    /// refused and nothing happens.
    /// </summary>
    public Task AddWorkspaceAsync(string folder) => LoadAsync(() =>
    {
        try
        {
            _workspaces.SetCurrent(_workspaces.Add(folder).Root);
        }
        catch (NestedWorkspaceException)
        {
            // Caught so the refusal does not take the app down from a background thread.
            // There is nowhere to report it yet. Give it one.
        }
    });

    public Task SwitchToAsync(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return workspace.CanSwitch
            ? LoadAsync(() => _workspaces.SetCurrent(workspace.Workspace.Root))
            : Task.CompletedTask;
    }

    /// <summary>
    /// Whether the window this is showing in is the one in front. False stops everything
    /// behind the git strip until it comes back.
    /// </summary>
    public void SetActive(bool active) => _git.IsActive = active;

    /// <summary>
    /// Lets go of the monitor. The container owns the monitor itself and disposes it, so
    /// this only takes back what this class added to it.
    /// </summary>
    public void Dispose()
    {
        _git.Changed -= OnGitChanged;
        Engines.InstallsChanged -= OnInstallsChanged;
    }

    private void OnGitChanged(object? sender, EventArgs e) =>
        _dispatcher.Post(() => Git.Status = _git.Status);

    /// <summary>
    /// Brings the open workspace's repository up to date, then reads it again so the counts
    /// move without waiting for the monitor to come round.
    /// </summary>
    /// <remarks>
    /// One button and one word for two outcomes. It always fetches, and it takes the new
    /// commits when there is nothing local that taking them could cost. Which of the two
    /// happened is visible in the strip either way, since behind either went to zero or did
    /// not. See <see cref="IGitUpdater"/> for what makes the second safe.
    /// <para>
    /// A failed update says nothing, because there is nowhere to say it yet. The same gap as
    /// a refused nested workspace, and it closes the same way, with an error surface.
    /// </para>
    /// <para>
    /// The generated command refuses to run twice at once, so the button is out of action
    /// for as long as git is, and there is no guard here doing the same job.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task Update()
    {
        if (_workspaces.Current is not { } current)
        {
            return;
        }

        Git.IsUpdating = true;

        try
        {
            await _updater.UpdateAsync(current.Root);
            await _git.RefreshAsync();
        }
        finally
        {
            Git.IsUpdating = false;
        }
    }

    /// <summary>
    /// Does a piece of work that touches disk, then reads the list back, both away from the
    /// UI thread, and puts the answer on screen when it lands.
    /// </summary>
    /// <remarks>
    /// Every one of these touches disk more than it looks like. Adding a workspace creates
    /// directories and writes files, opening one rewrites application state, and reading the
    /// list back resolves a name for each workspace, which reads a config file and can search
    /// four levels of a project folder. On a warm page cache that is a few milliseconds, and
    /// on a cold one, a busy disk or a network share there is no upper bound at all. None of
    /// it belongs in front of a frame.
    /// <para>
    /// One at a time. Two of these overlapping would race on what the registry holds, and the
    /// second would show a list built from the first's half finished write.
    /// </para>
    /// </remarks>
    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(OnWorkspacePage));
        OnPropertyChanged(nameof(OnEnginesPage));
    }

    private async Task LoadAsync(Action? work = null)
    {
        if (!await _loading.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            // Everything the disk answers, gathered into a value before coming back. The
            // registry is read here rather than on the other side, so the UI thread never
            // asks it anything.
            var loaded = await Task.Run(() =>
            {
                work?.Invoke();
                return Read();
            }).ConfigureAwait(true);

            Apply(loaded);
        }
        finally
        {
            _loading.Release();
        }

        // After the release, so a refresh can never be the thing holding the load open.
        await RefreshEngineAsync().ConfigureAwait(true);
    }

    // The disk half. Everything here either reads a disk or hands a folder to something that
    // will, and nothing here touches a bound property, so it belongs away from the UI thread.
    private Loaded Read()
    {
        // Names and states are read from disk again, so a renamed project or a folder that
        // has gone missing shows up without anyone maintaining a list.
        _workspaces.Refresh();

        var current = _workspaces.Current;

        // The strip belongs to whichever workspace is open, so switching moves it. Here
        // rather than with the rest of the update, because letting go of a folder disposes
        // the watches on it. A missing folder is not followed, since running git in a folder
        // that is gone answers nothing.
        _git.Follow(current is { IsMissing: false } ? current.Root : null);

        return new Loaded(
            [.. _workspaces.All.Select(workspace => new WorkspaceViewModel(
                workspace,
                workspace.Root == current?.Root,
                _paths.Shorten(workspace.Root, RowPathLength)))],
            current?.Name ?? "No workspace",
            current is null
                ? "Add one to get started"
                : _paths.Shorten(current.Root, SubtitleLength));
    }

    // Back on the UI thread, since the await above captured it. Nothing here touches disk.
    private void Apply(Loaded loaded)
    {
        Workspaces.Clear();

        foreach (var workspace in loaded.Workspaces)
        {
            Workspaces.Add(workspace);
        }

        HasWorkspaces = Workspaces.Count > 0;
        WorkspaceName = loaded.Name;
        WorkspaceSubtitle = loaded.Subtitle;

        // Follow read on its own thread, so it has nothing to say yet when it moved and has
        // not moved at all when the workspace is the same one. Taking what it holds covers
        // both, where blanking this would leave the strip empty in the second case with
        // nothing coming to fill it.
        Git.Status = _git.Status;
    }

    /// <summary>
    /// Rebuilds the engine strip from what is on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every part of it is a disk read and one of them can run a process, so all of it is
    /// gathered on the thread pool and only the finished view model touches a bound
    /// property. That is the same split <see cref="Read"/> and <see cref="Apply"/> make.
    /// </para>
    /// <para>
    /// **Runs one at a time and waits its turn rather than giving up.** Dropping the
    /// second would leave the strip describing the workspace that was open a moment ago,
    /// and switching twice quickly is exactly when that happens. Each turn reads which
    /// workspace is open again, so the last one to run is the one that is right.
    /// </para>
    /// </remarks>
    private async Task RefreshEngineAsync()
    {
        await _reading.WaitAsync().ConfigureAwait(true);

        try
        {
            if (_workspaces.Current is not { IsMissing: false } current)
            {
                Engine = EngineViewModel.NoWorkspace;

                return;
            }

            var root = current.Root;
            var requirement = await Task.Run(() => _requirements.Read(root)).ConfigureAwait(true);
            var installed = await _engines.ReadAsync(CancellationToken.None).ConfigureAwait(true);
            var theDefault = await Task.Run(() => _godot.DefaultEngine).ConfigureAwait(true);

            Engine = new EngineViewModel(
                _resolver.Resolve(requirement, installed, theDefault),
                _paths,
                this);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or EngineStoreException or SettingsFileUnreadableException)
        {
            // A disk that will not answer leaves the strip saying nothing rather than
            // taking the window down. Everything else on the page still works.
            Engine = EngineViewModel.NoWorkspace;
        }
        finally
        {
            _reading.Release();
        }
    }

    /// <summary>
    /// Shows the progress dialog while a project opens. Set by the window, since a dialog
    /// belongs to one and a view model has none.
    /// </summary>
    /// <remarks>
    /// Answers true when the finished dialog was told to open the editor, which only a
    /// rebuild ever offers.
    /// </remarks>
    public Func<GodotProject, GodotLaunchMode, Func<IProgress<GodotLaunchStep>, CancellationToken, Task>, Task<bool>>? Launching { get; set; }

    /// <summary>Reports a failed open. Set by the window, for the same reason.</summary>
    public Func<GodotLaunchException, GodotLaunchMode, Task>? Failed { get; set; }

    /// <summary>
    /// Opens a project in an engine, building its C# and importing its assets first when
    /// either is needed.
    /// </summary>
    /// <remarks>
    /// **The wait is the reason there is a dialog.** A first open on a fresh clone imports
    /// every asset, which is minutes, and a C# build is not instant either. Neither is
    /// something to do behind a button that has already sprung back. Playing waits on the
    /// same two steps, since a project that will not build will not run.
    ///
    /// Without a window to show one in, the same work still runs and only the reporting is
    /// missing. That is the case a rendering harness composes.
    /// </remarks>
    public async Task OpenInGodot(InstalledEngine engine, GodotProject project, GodotLaunchMode mode)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(project);

        Task Work(IProgress<GodotLaunchStep> progress, CancellationToken cancellation) =>
            Task.Run(
                () => _godotLauncher.OpenAsync(
                    engine, project, mode, new ShortenedDetail(progress, _paths), cancellation),
                cancellation);

        var openAfter = false;

        try
        {
            if (Launching is { } show)
            {
                openAfter = await show(project, mode, Work);
            }
            else
            {
                await Work(new Progress<GodotLaunchStep>(), CancellationToken.None);
            }
        }
        catch (GodotLaunchException exception)
        {
            if (Failed is { } report)
            {
                await report(exception, mode);
            }

            return;
        }

        // A rebuild that ended with Open in Editor pressed. It goes the ordinary way
        // rather than starting the editor here, so the build is confirmed to still be
        // good rather than assumed from a moment ago. Editor mode never answers true, so
        // this cannot go round again.
        if (openAfter)
        {
            await OpenInGodot(engine, project, GodotLaunchMode.Editor);
        }
    }

    /// <summary>Shows a folder in the file browser. Off the UI thread, like every open is.</summary>
    public void OpenFolder(string directory) => Task.Run(() =>
    {
        try
        {
            _platform.OpenInFileBrowser(DirectoryLocation.Parse(directory));
        }
        catch (Exception exception) when (exception is ArgumentException
            or DirectoryNotFoundException or InvalidOperationException)
        {
        }
    });

    /// <summary>
    /// Opens the engines page. A version sends it to the available list already filtered
    /// to what would answer, since arriving at 183 releases and a search field is being
    /// shown the haystack.
    /// </summary>
    public void ShowEngines(string? version = null)
    {
        if (version is { Length: > 0 })
        {
            Engines.ShowAvailable(version);
        }

        Page = 1;
    }

    /// <summary>
    /// Installs the engine this workspace asks for, on the engines page so the install is
    /// visible while it runs.
    /// </summary>
    public Task InstallEngineAsync(EngineVersionPattern wanted, bool mono)
    {
        Page = 1;

        return Engines.InstallForAsync(wanted, mono);
    }

    private void OnInstallsChanged(object? sender, EventArgs e) => _ = RefreshEngineAsync();

    /// <summary>
    /// Writes a path in a step's detail line for the width it is shown in.
    /// </summary>
    /// <remarks>
    /// A path is shortened wherever one is shown, and the launcher reports one while it
    /// imports. It cannot do this itself, since how wide a line is belongs to whatever
    /// draws it. Only a rooted path is touched, which is the only kind it ever reports.
    /// </remarks>
    private sealed class ShortenedDetail : IProgress<GodotLaunchStep>
    {
        /// <summary>Roughly what fits the dialog's mono line at 420 wide.</summary>
        private const int DetailLength = 46;

        private readonly IProgress<GodotLaunchStep> _inner;
        private readonly IPathShortener _paths;

        public ShortenedDetail(IProgress<GodotLaunchStep> inner, IPathShortener paths)
        {
            _inner = inner;
            _paths = paths;
        }

        public void Report(GodotLaunchStep value) =>
            _inner.Report(Path.IsPathRooted(value.Detail)
                ? value with { Detail = _paths.Shorten(value.Detail, DetailLength) }
                : value);
    }

    /// <summary>Everything one load read off disk, ready to be put on screen.</summary>
    private sealed record Loaded(
        IReadOnlyList<WorkspaceViewModel> Workspaces,
        string Name,
        string Subtitle);
}
