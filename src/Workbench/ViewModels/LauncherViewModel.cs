using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.IO;
using Workbench.Core.Workspaces;

namespace Workbench.ViewModels;

public partial class LauncherViewModel : ViewModelBase
{
    /// <summary>Roughly what fits the selector and a switcher row at their fixed widths.</summary>
    private const int SubtitleLength = 34;
    private const int RowPathLength = 48;

    private readonly IWorkspaceRegistry _workspaces;
    private readonly IPathShortener _paths;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkspaceChevronAngle))]
    private bool _workspacesOpen;

    [ObservableProperty]
    private EngineViewModel _engine = EngineViewModel.For(EngineStatus.Matched);

    [ObservableProperty]
    private string _workspaceName = "No workspace";

    [ObservableProperty]
    private string _workspaceSubtitle = "Add one to get started";

    [ObservableProperty]
    private string _toolsSummary = "1 installed";

    /// <summary>False shows the empty state instead of the whole body.</summary>
    [ObservableProperty]
    private bool _hasWorkspaces;

    public LauncherViewModel(IWorkspaceRegistry workspaces, IPathShortener paths)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(paths);

        _workspaces = workspaces;
        _paths = paths;
        ReloadWorkspaces();
    }

    public string GitBranch => "feature/heat-rebalance";

    public string GitAhead => "2";

    public string GitBehind => "0";

    public string GitFetched => "4m";

    public double WorkspaceChevronAngle => WorkspacesOpen ? 180 : 0;

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    public IReadOnlyList<ToolCardViewModel> Tools { get; } =
    [
        new ToolCardViewModel
        {
            Mark = "F",
            Name = "Foundry",
            State = "INSTALLED",
            Version = "0.9.2",
            Description =
                "Author attributes, stats, effects, machines and recipes. "
                + "Also the graphs: pure, exec, state machines and behavior trees.",
        },
    ];

    public IReadOnlyList<GitStatViewModel> GitStats { get; } =
    [
        new GitStatViewModel("12", "modified", "#e0a943"),
        new GitStatViewModel("3", "staged", "#52cfa5"),
        new GitStatViewModel("1", "conflict", "#ea5257"),
    ];

    /// <summary>
    /// Registers a folder and opens it. A folder inside a workspace already added is
    /// refused and nothing happens.
    /// </summary>
    public void AddWorkspace(string folder)
    {
        Workspace added;

        try
        {
            added = _workspaces.Add(folder);
        }
        catch (NestedWorkspaceException)
        {
            // Caught only so the refusal does not take the app down from an async void
            // handler. There is nowhere to report it yet. Give it one.
            return;
        }

        _workspaces.SetCurrent(added.Root);

        ReloadWorkspaces();
        WorkspacesOpen = false;
    }

    public void SwitchTo(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (!workspace.CanSwitch)
        {
            return;
        }

        _workspaces.SetCurrent(workspace.Workspace.Root);

        ReloadWorkspaces();
        WorkspacesOpen = false;
    }

    [RelayCommand]
    private void ToggleWorkspaces() => WorkspacesOpen = !WorkspacesOpen;

    [RelayCommand]
    private void CloseWorkspaces() => WorkspacesOpen = false;

    // Names and states are read from disk again, so a renamed project or a folder
    // that has gone missing shows up without anyone maintaining a list.
    private void ReloadWorkspaces()
    {
        _workspaces.Refresh();

        var current = _workspaces.Current;

        Workspaces.Clear();

        foreach (var workspace in _workspaces.All)
        {
            Workspaces.Add(new WorkspaceViewModel(
                workspace,
                workspace.Root == current?.Root,
                _paths.Shorten(workspace.Root, RowPathLength)));
        }

        HasWorkspaces = Workspaces.Count > 0;
        WorkspaceName = current?.Name ?? "No workspace";
        WorkspaceSubtitle = current is null
            ? "Add one to get started"
            : _paths.Shorten(current.Root, SubtitleLength);
        ToolsSummary = current is null ? "1 installed" : $"1 installed, scoped to {current.Name}";
    }
}