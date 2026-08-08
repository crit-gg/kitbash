using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Git;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.Ui;
using Kitbash.Ui.Toasts;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

public partial class LauncherViewModel : ViewModelBase, IDisposable
{
    /// <summary>Roughly what fits the selector and a switcher row at their fixed widths.</summary>
    private const int SubtitleLength = 34;
    private const int RowPathLength = 48;

    private readonly IWorkspaceRegistry _workspaces;
    private readonly IWorkspacesSettings _workspaceSettings;
    private readonly IWorkspaceLinks _links;
    private readonly IPathShortener _paths;
    private readonly IGitStatusMonitor _git;
    private readonly IGitUpdater _updater;
    private readonly IGitCloner _cloner;
    private readonly IUiDispatcher _dispatcher;
    private readonly IEngineRequirementReader _requirements;
    private readonly IEngineStore _engines;
    private readonly IEngineCatalogue _catalogue;
    private readonly IEngineResolver _resolver;
    private readonly IGodotSettings _godot;
    private readonly IGodotLauncher _godotLauncher;
    private readonly IGodotProjectReader _projects;
    private readonly IWorkspaceMaker _maker;
    private readonly IInstalledTools _installed;
    private readonly IProvidedTools _provided;
    private readonly IToolStarter _starter;
    private readonly IToolScriptRunner _scripts;
    private readonly IToolInputMemory _memory;
    private readonly IToolCatalogue _tools;
    private readonly IToolInstaller _installer;
    private readonly IToolFolderInstaller _folders;
    private readonly ToolIconImages _images;
    private readonly ToolLog _log;
    private readonly IFileSystem _files;
    private readonly IToastService _toasts;
    private readonly IPlatformServices _platform;
    private readonly IAfterLaunchSettings _afterLaunch;
    private readonly IAfterLaunchActions _actions;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private readonly SemaphoreSlim _reading = new(1, 1);
    private readonly GitHeadTracker _head = new();

    private IReadOnlyList<ToolGroupViewModel> _toolGroups = [];
    private IReadOnlyList<OfferedTool> _offers = [];
    private bool _swept;

    /// <summary>Counts the refreshes, so a draw from a superseded one is thrown away.</summary>
    private int _drawing;

    /// <summary>Which workspace the offers were read for, since a list is per workspace.</summary>
    private string? _offersFor;

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
        IWorkspacesSettings workspaceSettings,
        IWorkspaceLinks links,
        IPathShortener paths,
        IInstalledTools installed,
        IProvidedTools provided,
        IToolStarter starter,
        IToolScriptRunner scripts,
        IToolInputMemory memory,
        IToolCatalogue tools,
        IToolInstaller installer,
        IToolFolderInstaller folders,
        ToolIconImages images,
        ToolLog log,
        IFileSystem files,
        IGitStatusMonitor git,
        IGitUpdater updater,
        IGitCloner cloner,
        IUiDispatcher dispatcher,
        IEngineRequirementReader requirements,
        IEngineStore engineStore,
        IEngineCatalogue engineCatalogue,
        IEngineResolver resolver,
        IGodotSettings godot,
        IGodotLauncher godotLauncher,
        IGodotProjectReader projects,
        IWorkspaceMaker maker,
        IToastService toasts,
        IPlatformServices platform,
        IAfterLaunchSettings afterLaunch,
        IAfterLaunchActions actions,
        EnginesViewModel engines,
        OpenInViewModel openIn)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(workspaceSettings);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(provided);
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(cloner);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(engineStore);
        ArgumentNullException.ThrowIfNull(engineCatalogue);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(godotLauncher);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(maker);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(afterLaunch);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(openIn);

        _workspaces = workspaces;
        _workspaceSettings = workspaceSettings;
        _links = links;
        _paths = paths;
        _git = git;
        _updater = updater;
        _cloner = cloner;
        _dispatcher = dispatcher;
        _requirements = requirements;
        _engines = engineStore;
        _catalogue = engineCatalogue;
        _resolver = resolver;
        _godot = godot;
        _godotLauncher = godotLauncher;
        _projects = projects;
        _maker = maker;
        _installed = installed;
        _provided = provided;
        _starter = starter;
        _scripts = scripts;
        _memory = memory;
        _tools = tools;
        _installer = installer;
        _folders = folders;
        _images = images;
        _log = log;
        _files = files;
        _toasts = toasts;
        _platform = platform;
        _afterLaunch = afterLaunch;
        _actions = actions;

        Engines = engines;
        OpenIn = openIn;
        ToolCheck = new ToolCheckViewModel(() => RefreshToolsAsync(refresh: true));

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

        // Not part of the read above, which runs before the window exists and has to stay
        // quick. This one probes every install and can run a process.
        _ = RefreshEngineAsync();

        // Scans the tools directory, which is a disk read, so it happens off this thread
        // and the page fills in a moment after it opens.
        _ = RefreshToolsAsync();
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    /// <summary>
    /// What the open workspace points at, above the tools. A readout, so nothing here adds
    /// to it or takes anything off it.
    /// </summary>
    public ObservableCollection<WorkspaceLinkViewModel> Links { get; } = [];

    /// <summary>False leaves the whole section out, which is a workspace naming none.</summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>The engines page, which the rail's second item opens.</summary>
    public EnginesViewModel Engines { get; }

    /// <summary>The Open in menu, beside the engine strip's own button.</summary>
    public OpenInViewModel OpenIn { get; }

    public bool OnWorkspacePage => Page == 0;

    public bool OnEnginesPage => Page == 1;

    /// <summary>
    /// The repository the open workspace sits in, kept current by the monitor. Empty of a
    /// status when the workspace is not a repository, and the plain footer shows instead.
    /// </summary>
    public GitViewModel Git { get; } = new();

    /// <summary>
    /// What the tools page draws, which is what is installed on this machine. Nothing
    /// offers a tool to install yet, so there is one group rather than two.
    /// </summary>
    public IReadOnlyList<ToolGroupViewModel> ToolGroups
    {
        get => _toolGroups;
        private set
        {
            SetProperty(ref _toolGroups, value);
            OnPropertyChanged(nameof(HasTools));
        }
    }

    /// <summary>False shows the empty state instead of the cards.</summary>
    public bool HasTools => ToolGroups.Count > 0;

    /// <summary>
    /// Check for updates, which the first heading and the empty state both draw. A
    /// repository list with nothing to offer yet is the case that needs the empty one.
    /// </summary>
    public ToolCheckViewModel ToolCheck { get; }

    /// <summary>
    /// Reads what is installed, draws it, then asks the repositories and draws again. Two
    /// passes because the disk answers at once and a network does not, and a person who
    /// has a tool should not wait on a server to see it.
    /// </summary>
    /// <param name="refresh">Asks every repository now rather than using what was cached.</param>
    public async Task RefreshToolsAsync(bool refresh = false)
    {
        // Switching workspaces starts one of these, so a person switching twice quickly has
        // two running. The later one wins and the earlier one stops drawing.
        var token = ++_drawing;
        var root = _workspaces.Current?.Root;

        if (!string.Equals(root, _offersFor, StringComparison.Ordinal))
        {
            // An offer belongs to the workspace whose list brought it in, so it says nothing
            // here and the first pass draws what is installed alone.
            _offers = [];
        }

        var (installed, provided) = await Task.Run(Provided).ConfigureAwait(true);

        if (!_swept)
        {
            // At launch, since a version is never removed while anything might be running
            // from it. A failed install leaves its own leftovers here too. Every tool here,
            // not only the ones this workspace provides, since the folders are the machine's.
            _swept = true;
            _ = Task.Run(() => _installer.SweepOldVersions(installed));
        }

        if (token != _drawing)
        {
            return;
        }

        Draw(provided, _offers);

        var offers = await ReadOffersAsync(refresh).ConfigureAwait(true);

        if (token != _drawing)
        {
            return;
        }

        // An install made before the origin was recorded has none, and so is on the page
        // everywhere. The repository answering for it now is what fills that in.
        if (installed.Any(tool => tool.Origin is null))
        {
            var known = installed;

            provided = await Task.Run(() =>
            {
                Adopt(known, offers);

                return Provided().Provided;
            }).ConfigureAwait(true);

            if (token != _drawing)
            {
                return;
            }
        }

        _offers = offers;
        _offersFor = root;

        Draw(provided, offers);
    }

    /// <summary>What is installed and which of it this workspace provides. Reads a disk.</summary>
    private (IReadOnlyList<InstalledTool> Installed, IReadOnlyList<ProvidedTool> Provided) Provided()
    {
        var installed = _installed.Read();

        return (installed, _provided.Here(installed));
    }

    /// <summary>
    /// Records where an install that recorded nothing came from, from the repository that is
    /// offering it now. A linked tool is never one of these, since a local id matches no
    /// repository's. Writes a file, so it runs off the UI thread.
    /// </summary>
    private void Adopt(IReadOnlyList<InstalledTool> installed, IReadOnlyList<OfferedTool> offers)
    {
        var offered = offers.ToDictionary(offer => offer.Id.Value, StringComparer.Ordinal);

        foreach (var tool in installed.Where(tool => tool.Origin is null))
        {
            if (offered.TryGetValue(tool.Id.Value, out var offer))
            {
                _installed.SetOrigin(tool.Id, offer.Source);
            }
        }
    }

    private async Task<IReadOnlyList<OfferedTool>> ReadOffersAsync(bool refresh)
    {
        try
        {
            return await _tools.ReadAsync(refresh, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing a repository does reaches a person, so a fault here leaves the page
            // showing what is installed and says nothing.
            _log.Say("the tool catalogue could not be read", exception);

            return _offers;
        }
    }

    private void Draw(IReadOnlyList<ProvidedTool> installed, IReadOnlyList<OfferedTool> offers)
    {
        var offered = offers.ToDictionary(offer => offer.Id.Value, StringComparer.Ordinal);
        var held = installed.Select(tool => tool.Tool.Id.Value).ToHashSet(StringComparer.Ordinal);

        // A script is its own section on both sides, since running one and opening an app
        // are different things to want, and a script card is a different shape.
        List<ToolCardViewModel> mine = [];
        List<ToolCardViewModel> scripts = [];

        foreach (var (tool, workspaces) in installed)
        {
            var card = new ToolCardViewModel(
                tool,
                offered.GetValueOrDefault(tool.Id.Value),
                InstallToolAsync,
                menu: MenuFor(tool))
            {
                IsCompact = tool.Manifest.IsScript,
                ProvidedBy = workspaces,
            };

            (tool.Manifest.IsScript ? scripts : mine).Add(card);
        }

        List<ToolCardViewModel> theirs = [];
        List<ToolCardViewModel> theirScripts = [];

        foreach (var offer in offers.Where(offer => !held.Contains(offer.Id.Value)))
        {
            var card = new ToolCardViewModel(null, offer, InstallToolAsync)
            {
                IsCompact = offer.Manifest.IsScript,
            };

            (offer.Manifest.IsScript ? theirScripts : theirs).Add(card);
        }

        List<ToolGroupViewModel> groups = [];

        if (mine.Count > 0)
        {
            groups.Add(new ToolGroupViewModel("INSTALLED TOOLS", mine, offersUpdates: true, ToolCheck));
        }

        if (scripts.Count > 0)
        {
            groups.Add(new ToolGroupViewModel("INSTALLED SCRIPTS", scripts, offersUpdates: true, ToolCheck));
        }

        if (theirs.Count > 0)
        {
            groups.Add(new ToolGroupViewModel(
                "AVAILABLE TOOLS", theirs, offersUpdates: false, ToolCheck));
        }

        if (theirScripts.Count > 0)
        {
            groups.Add(new ToolGroupViewModel(
                "AVAILABLE SCRIPTS", theirScripts, offersUpdates: false, ToolCheck));
        }

        // The page has no bar of its own, so its actions ride on the first heading.
        // Whichever group that is, since a person with nothing installed still needs it.
        if (groups.Count > 0)
        {
            groups[0].ShowsPageMenu = true;
            groups[0].ShowsCheck = true;
        }

        ToolGroups = groups;

        // The art follows the card rather than holding it up, since an offered tool's icon
        // is a download the first time anything asks for it.
        _ = LoadIconsAsync([.. mine, .. scripts, .. theirs, .. theirScripts]);
    }

    /// <summary>
    /// Fills in each card's icon once the page is on screen. A tool installed before it
    /// published one has nothing in its folder, so the offer supplies it until the update.
    /// </summary>
    private async Task LoadIconsAsync(IReadOnlyList<ToolCardViewModel> cards)
    {
        foreach (var card in cards)
        {
            var image = card.Tool is { } tool
                ? await _images.ForAsync(tool).ConfigureAwait(true)
                : null;

            image ??= card.Offer is { } offer
                ? await _images.ForAsync(offer, CancellationToken.None).ConfigureAwait(true)
                : null;

            card.Icon = image;
        }
    }

    /// <summary>
    /// Puts the offered version on this machine, which is Install for a tool nobody has
    /// and Update for one somebody does. The page is read again either way.
    /// </summary>
    private async Task InstallToolAsync(ToolCardViewModel card)
    {
        if (card.Offer is not { } offer)
        {
            return;
        }

        var progress = new Progress<ToolInstallProgress>(step => card.Report(Percent(step)));

        try
        {
            await Task.Run(() => _installer.InstallAsync(offer, progress, CancellationToken.None))
                .ConfigureAwait(true);

            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Ok,
                Title = $"{offer.Name} {offer.Version} is installed",
            });
        }
        catch (ToolInstallException exception)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = $"{offer.Name} could not be installed",
                Body = exception.Message,
            });
        }

        await RefreshToolsAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Adds a tool from a folder on this machine. The folder is not copied, so a rebuild
    /// in it is picked up without installing again.
    /// </summary>
    public async Task InstallToolFromFolderAsync(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        try
        {
            var tool = await Task.Run(() => _folders.InstallFrom(folder)).ConfigureAwait(true);

            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Ok,
                Title = $"{tool.Name} {tool.Version} was added",
                Body = "It runs from the folder you picked, so a rebuild there needs nothing here.",
            });
        }
        catch (ToolInstallException exception)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = "That folder does not hold a tool",
                Body = exception.Message,
            });
        }

        await RefreshToolsAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// The bar across the whole install rather than across one stage, so it only ever goes
    /// forwards. The download is nearly all of the wait, so it takes nearly all of the bar.
    /// </summary>
    private static double Percent(ToolInstallProgress step) => step.Stage switch
    {
        ToolInstallStage.Downloading => (step.Fraction ?? 0) * 90,
        ToolInstallStage.Verifying => 92,
        ToolInstallStage.Extracting => 96,
        _ => 100,
    };

    /// <summary>What a tool's installed version takes up, written for a person to read.</summary>
    public string SizeOnDisk(InstalledTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var bytes = _files
            .EnumerateFiles(tool.Directory, recursive: true)
            .Sum(_files.GetFileLength);

        return $"{EngineRowViewModel.Size(bytes)} on disk";
    }

    /// <summary>Removes every version and keeps the tool's settings and its state.</summary>
    public async Task UninstallToolAsync(InstalledTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        try
        {
            await Task.Run(() => _installer.Uninstall(tool)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = $"{tool.Name} could not be removed",
                Body = exception.Message,
            });
        }

        await RefreshToolsAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Asks the form for what a script needs. Set by the window, since a dialog belongs to
    /// one and a view model has none.
    /// </summary>
    public Func<ToolInputsViewModel, Task<bool>>? AskingInputs { get; set; }

    /// <summary>Shows the progress dialog while a script runs. Set by the window too.</summary>
    public Func<string, Func<IProgress<ToolProgressStep>, CancellationToken, Task<ToolRunOutcome>>, Task<ToolRunOutcome?>>? RunningScript { get; set; }

    /// <summary>
    /// Opens a tool. It outlives the launcher, so a person can close this window and keep
    /// working, and a tool that will not start says so as a toast rather than in place.
    /// A script tool is run and watched instead, so that one does not outlive anything.
    /// </summary>
    public async Task LaunchToolAsync(InstalledTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.Manifest.IsScript)
        {
            await RunScriptAsync(tool).ConfigureAwait(true);

            return;
        }

        var workspace = _workspaces.Current?.Root;

        try
        {
            await Task.Run(() => _starter.Start(tool, workspace)).ConfigureAwait(true);

            // Only once the tool really started, so a start that failed leaves the
            // launcher up with its toast on screen.
            _actions.Apply(_afterLaunch.ForTool(tool.Id));
        }
        catch (ProcessStartException exception)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = $"{tool.Name} could not start",
                Body = exception.Message,
            });
        }
    }

    /// <summary>
    /// Runs a script tool: the form first when it asks for anything, then the script under
    /// a modal that reports what it writes. Cancelling either one stops there.
    /// </summary>
    private async Task RunScriptAsync(InstalledTool tool)
    {
        var workspace = _workspaces.Current?.Root;
        IReadOnlyList<string> arguments = [];

        if (tool.Manifest.Inputs.Count > 0)
        {
            if (AskingInputs is not { } ask)
            {
                return;
            }

            var form = new ToolInputsViewModel(tool, _memory.Read(tool.Id, tool.Manifest.Inputs), workspace);

            if (!await ask(form).ConfigureAwait(true))
            {
                return;
            }

            // Kept before the run rather than after it, so a script that fails still finds
            // its form as it was left.
            _memory.Write(tool.Id, tool.Manifest.Inputs, form.Values);

            arguments = form.Arguments();
        }

        if (RunningScript is not { } run)
        {
            return;
        }

        Task<ToolRunOutcome> Work(IProgress<ToolProgressStep> progress, CancellationToken cancellation) =>
            _scripts.RunAsync(tool, workspace, arguments, progress, cancellation);

        try
        {
            // A failed run reports itself in the dialog, which is the only place its log
            // is, so nothing is said here about one.
            if (await run(tool.Name, Work).ConfigureAwait(true) is { Worked: true })
            {
                _toasts.Post(new ToastRequest
                {
                    Tier = ToastTier.Ok,
                    Title = $"{tool.Name} finished",
                });
            }
        }
        catch (ProcessStartException exception)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = $"{tool.Name} could not start",
                Body = exception.Message,
            });
        }
    }

    /// <summary>
    /// What a card's menu holds. One entry, until installing and uninstalling land.
    /// </summary>
    private IReadOnlyList<ToolMenuItemViewModel> MenuFor(InstalledTool tool) =>
        [new ToolMenuItemViewModel("Open folder", () => OpenFolder(tool.Directory))];

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

    /// <summary>
    /// A clone about to be set up, with this machine's usual folder already in it. The
    /// dialog owns it, since nothing outside one is cloning.
    /// </summary>
    public CloneWorkspaceViewModel NewClone() =>
        new(_cloner, _workspaceSettings.DefaultDirectory);

    /// <summary>
    /// A workspace about to be described, with this machine's usual folder already in it.
    /// The dialog owns it, the way the clone dialog owns its own.
    /// </summary>
    public NewWorkspaceViewModel NewWorkspace() =>
        new(_maker, _engines, _catalogue, _godot, _workspaceSettings.DefaultDirectory);

    /// <summary>
    /// Makes a workspace, opens it, and opens its project in the editor when it has one.
    /// An engine that is not installed is installed first, on the engines page, since that
    /// is where an install already reports its progress.
    /// </summary>
    public async Task CreateWorkspaceAsync(NewWorkspace request, bool needsInstall)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (needsInstall && request.Engine is { } wanted)
        {
            // The runtime the dialog picked, since the workspace is pinned to that build.
            await InstallEngineAsync(EngineVersionPattern.Parse(wanted.Tag.ToString()), mono: wanted.IsMono)
                .ConfigureAwait(true);
        }

        Workspace made;

        try
        {
            made = await _maker.MakeAsync(request).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or NestedWorkspaceException)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = "The workspace could not be created",
                Body = exception.Message,
            });

            return;
        }

        // Back to the workspace page, since the install above may have left the engines
        // page open, and the new workspace is what there is to look at now.
        Page = 0;

        await LoadAsync().ConfigureAwait(true);

        if (request.HasGodotProject)
        {
            await OpenNewProjectAsync(made.Root).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Opens the project just made. The engine is resolved the ordinary way, so a version
    /// whose install did not finish leaves the strip to offer it rather than failing here.
    /// </summary>
    private async Task OpenNewProjectAsync(string root)
    {
        var project = await Task.Run(() => _projects.Find(root)).ConfigureAwait(true);

        if (project is null)
        {
            return;
        }

        var requirement = await Task.Run(() => _requirements.Read(root)).ConfigureAwait(true);
        var installed = await _engines.ReadAsync(CancellationToken.None).ConfigureAwait(true);
        var theDefault = await Task.Run(() => _godot.DefaultEngine).ConfigureAwait(true);

        if (_resolver.Resolve(requirement, installed, theDefault).Engine is { } engine)
        {
            await OpenInGodot(engine, project, GodotLaunchMode.Editor).ConfigureAwait(true);
        }
    }

    /// <summary>Takes a workspace out of the list. The folder on disk is left alone.</summary>
    public Task RemoveWorkspaceAsync(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return LoadAsync(() => _workspaces.Remove(workspace.Workspace.Root));
    }

    /// <summary>Names a workspace for this person. Blank goes back to the derived name.</summary>
    public Task RenameWorkspaceAsync(WorkspaceViewModel workspace, string? name)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return LoadAsync(() =>
        {
            try
            {
                _workspaces.Rename(workspace.Workspace.Root, name);
            }
            catch (Exception exception) when (exception is SettingsFileUnreadableException
                or IOException or UnauthorizedAccessException)
            {
                // Caught so a file that will not take the name does not take the app down
                // from a background thread. There is nowhere to report it yet. Give it one.
            }
        });
    }

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
    /// Reads the Open in menu again. The settings window hides a tool and adds one, and
    /// neither shows up until this runs, since the menu is gathered rather than watched.
    /// </summary>
    public Task RefreshOpenInAsync() =>
        OpenIn.RefreshAsync(_workspaces.Current is { IsMissing: false } current ? current.Root : null);

    /// <summary>
    /// Lets go of the monitor. The container owns the monitor itself and disposes it, so
    /// this only takes back what this class added to it.
    /// </summary>
    public void Dispose()
    {
        _git.Changed -= OnGitChanged;
        Engines.InstallsChanged -= OnInstallsChanged;
    }

    private void OnGitChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        var status = _git.Status;

        Git.Status = status;

        // The workspace's config travels in the repository, so switching branch or taking
        // in commits can change its name, its links, the engine it asks for and the tools
        // it offers. Everything else the monitor reports is the working tree, which the
        // strip draws and nothing else follows.
        if (_head.Moved(status))
        {
            // Waits for a load already running rather than giving up on this one. Nothing
            // else would come round to notice, since the monitor speaks on a difference and
            // the head has already been taken.
            _ = LoadAsync(waits: true);
        }
    });

    /// <summary>
    /// Brings the open workspace's repository up to date, then reads it again so the counts
    /// move without waiting for the monitor to come round.
    /// </summary>
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

    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(OnWorkspacePage));
        OnPropertyChanged(nameof(OnEnginesPage));
    }

    /// <summary>
    /// Does a piece of work that touches disk, then reads the list back, both away from the
    /// UI thread, and puts the answer on screen when it lands.
    /// </summary>
    /// <param name="work">Done on the other thread before the list is read back.</param>
    /// <param name="waits">
    /// Takes its turn behind a load already running instead of giving up. For a caller
    /// that is following something rather than answering a gesture.
    /// </param>
    private async Task LoadAsync(Action? work = null, bool waits = false)
    {
        if (waits)
        {
            await _loading.WaitAsync().ConfigureAwait(true);
        }
        else if (!await _loading.WaitAsync(0).ConfigureAwait(true))
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

        // The page belongs to whichever workspace is open, since a workspace's own list
        // offers a tool there alone, so switching moves it the way it moves the strips.
        await RefreshToolsAsync().ConfigureAwait(true);
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
            // The workspace's own file, so the set changes with the workspace the way the
            // git and engine strips do.
            [.. _links.Read().Select(link => new WorkspaceLinkViewModel(link, OpenLink))],
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

        Links.Clear();

        foreach (var link in loaded.Links)
        {
            Links.Add(link);
        }

        OnPropertyChanged(nameof(HasLinks));

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
    /// Hands a workspace link to the desktop. Starting the browser can block, so it runs
    /// off the UI thread the way the release notes do.
    /// </summary>
    private void OpenLink(WorkspaceLink link) => Task.Run(() =>
    {
        try
        {
            _platform.OpenInBrowser(link.Address);
        }
        catch (Exception exception) when (exception is ProcessStartException or ArgumentException)
        {
            _toasts.Post(new ToastRequest
            {
                Tier = ToastTier.Error,
                Title = $"Could not open {link.Label}",
                Body = exception.Message,
            });
        }
    });

    /// <summary>
    /// Rebuilds the engine strip from what is on disk.
    /// </summary>
    private async Task RefreshEngineAsync()
    {
        await _reading.WaitAsync().ConfigureAwait(true);

        try
        {
            if (_workspaces.Current is not { IsMissing: false } current)
            {
                Engine = EngineViewModel.NoWorkspace;
                await OpenIn.RefreshAsync(null).ConfigureAwait(true);

                return;
            }

            var root = current.Root;

            // Gathered here rather than when the menu opens, so opening it touches no disk.
            await OpenIn.RefreshAsync(root).ConfigureAwait(true);

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
    public Func<GodotProject, GodotLaunchMode, Func<IProgress<GodotLaunchStep>, CancellationToken, Task>, Task<GodotLaunchOutcome>>? Launching { get; set; }

    /// <summary>Reports a failed open. Set by the window, for the same reason.</summary>
    public Func<GodotLaunchException, GodotLaunchMode, Task>? Failed { get; set; }

    /// <summary>
    /// Opens a project in an engine, building its C# and importing its assets first when
    /// either is needed.
    /// </summary>
    public async Task OpenInGodot(InstalledEngine engine, GodotProject project, GodotLaunchMode mode)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(project);

        Task Work(IProgress<GodotLaunchStep> progress, CancellationToken cancellation) =>
            Task.Run(
                () => _godotLauncher.OpenAsync(
                    engine, project, mode, new ShortenedDetail(progress, _paths), cancellation),
                cancellation);

        var outcome = GodotLaunchOutcome.Finished;

        try
        {
            if (Launching is { } show)
            {
                outcome = await show(project, mode, Work);
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
        // good rather than assumed from a moment ago. Editor mode never answers this, so
        // it cannot go round again, and the close below is left to the second pass.
        if (outcome == GodotLaunchOutcome.OpenEditor)
        {
            await OpenInGodot(engine, project, GodotLaunchMode.Editor);

            return;
        }

        // Only ever after something really started. A cancelled launch started nothing
        // and a rebuild starts nothing by design.
        if (outcome == GodotLaunchOutcome.Finished)
        {
            _actions.Apply(ActionAfter(mode));
        }
    }

    /// <summary>What this person asked the launcher to do once the mode has started.</summary>
    private AfterLaunchAction ActionAfter(GodotLaunchMode mode) => mode switch
    {
        GodotLaunchMode.Editor => _afterLaunch.AfterEditor,
        GodotLaunchMode.Play => _afterLaunch.AfterPlay,
        _ => AfterLaunchAction.DoNothing,
    };

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

    /// <summary>Opens the engines page, which is the rail's second item.</summary>
    public void ShowEngines() => Page = 1;

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
        IReadOnlyList<WorkspaceLinkViewModel> Links,
        string Name,
        string Subtitle);
}
