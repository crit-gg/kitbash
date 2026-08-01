using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core;
using Workbench.Core.Git;
using Workbench.Core.IO;
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
    private readonly SemaphoreSlim _loading = new(1, 1);

    [ObservableProperty]
    private bool _workspacesOpen;

    [ObservableProperty]
    private string _workspaceName = "No workspace";

    [ObservableProperty]
    private string _workspaceSubtitle = "Add one to get started";

    /// <summary>False shows the empty state instead of the whole page.</summary>
    [ObservableProperty]
    private bool _hasWorkspaces;



    public LauncherViewModel(
        IWorkspaceRegistry workspaces,
        IPathShortener paths,
        IToolRegistry tools,
        IGitStatusMonitor git,
        IGitUpdater updater,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _workspaces = workspaces;
        _paths = paths;
        _git = git;
        _updater = updater;
        _dispatcher = dispatcher;

        Tools = [.. tools.Tools.Select(tool => new ToolCardViewModel(tool))];

        // The monitor reads on its own threads, so what it says has to be carried over
        // before anything bound to it is touched.
        _git.Changed += OnGitChanged;

        // The one read that stays here. This runs before the window exists, so there is no
        // frame to drop and nothing to feel, and doing it now means the launcher opens
        // filled in rather than opening empty and filling in a moment later.
        Apply(Read());
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    /// <summary>
    /// The repository the open workspace sits in, kept current by the monitor. Empty of a
    /// status when the workspace is not a repository, and the plain footer shows instead.
    /// </summary>
    public GitViewModel Git { get; } = new();

    /// <summary>
    /// The engine strip. Invented for now, and the only invented data in the app. See
    /// <see cref="EngineViewModel"/> for what is already readable and what needs engine
    /// discovery.
    /// </summary>
    public EngineViewModel Engine { get; } = EngineViewModel.Placeholder;

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
    public void Dispose() => _git.Changed -= OnGitChanged;

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

    /// <summary>Everything one load read off disk, ready to be put on screen.</summary>
    private sealed record Loaded(
        IReadOnlyList<WorkspaceViewModel> Workspaces,
        string Name,
        string Subtitle);
}
