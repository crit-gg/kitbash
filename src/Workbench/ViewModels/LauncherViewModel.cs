using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core;
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
    private bool _workspacesOpen;

    [ObservableProperty]
    private string _workspaceName = "No workspace";

    [ObservableProperty]
    private string _workspaceSubtitle = "Add one to get started";

    /// <summary>False shows the empty state instead of the whole page.</summary>
    [ObservableProperty]
    private bool _hasWorkspaces;

    public LauncherViewModel(IWorkspaceRegistry workspaces, IPathShortener paths, IToolRegistry tools)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(tools);

        _workspaces = workspaces;
        _paths = paths;

        Tools = [.. tools.Tools.Select(tool => new ToolCardViewModel(tool))];

        ReloadWorkspaces();
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } = [];

    /// <summary>
    /// The registered tools. This is the registry's list rather than a written one, so
    /// the launcher shows what the app actually offers.
    /// </summary>
    public IReadOnlyList<ToolCardViewModel> Tools { get; }

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
    }
}
