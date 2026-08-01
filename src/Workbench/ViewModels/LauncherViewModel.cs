using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Workbench.ViewModels;

public partial class LauncherViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkspaceBorder))]
    [NotifyPropertyChangedFor(nameof(WorkspaceChevronAngle))]
    private bool _workspacesOpen;

    [ObservableProperty]
    private EngineViewModel _engine = EngineViewModel.For(EngineStatus.Matched);

    public string AppVersion => "0.9.2";

    public string WorkspaceName => "Overpressure";

    public string WorkspaceSubtitle => "D:/dev/overpressure/godot · 12 unsaved";

    public string ToolsSummary => "1 installed · scoped to Overpressure";

    public string WorkspacesNote => "one game project on disk";

    public string GitBranch => "feature/heat-rebalance";

    public string GitAhead => "2";

    public string GitBehind => "0";

    public string GitFetched => "4m";

    /// <summary>The workspace button takes an accent border while the list is open.</summary>
    public IBrush WorkspaceBorder =>
        WorkspacesOpen ? SolidColorBrush.Parse("#58a6f0") : SolidColorBrush.Parse("#313943");

    public double WorkspaceChevronAngle => WorkspacesOpen ? 180 : 0;

    public IReadOnlyList<ToolCardViewModel> Tools { get; } =
    [
        new ToolCardViewModel
        {
            Mark = "F",
            Name = "Foundry",
            State = "INSTALLED",
            Version = "0.9.2",
            Description =
                "Data editor — author attributes, stats, effects, machines and recipes, "
                + "plus the designer-facing graphs: pure, exec, state machines and behavior trees.",
        },
    ];

    public IReadOnlyList<GitStatViewModel> GitStats { get; } =
    [
        new GitStatViewModel("12", "modified", "#e0a943"),
        new GitStatViewModel("3", "staged", "#52cfa5"),
        new GitStatViewModel("1", "conflict", "#ea5257"),
    ];

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; } =
    [
        new WorkspaceViewModel
        {
            Name = "Overpressure",
            Path = "D:/dev/overpressure/godot",
            Badge = "OPEN",
            Action = "current",
            IsCurrent = true,
            Dot = SolidColorBrush.Parse("#52cfa5"),
            BadgeBackground = SolidColorBrush.Parse("#112019"),
            BadgeBorder = SolidColorBrush.Parse("#23452f"),
            BadgeForeground = SolidColorBrush.Parse("#7cd6b2"),
            ActionForeground = SolidColorBrush.Parse("#6e7982"),
        },
        new WorkspaceViewModel
        {
            Name = "Overpressure — demo build",
            Path = "D:/dev/overpressure-demo/godot",
            Badge = "READ ONLY",
            Action = "switch →",
            IsCurrent = false,
            Dot = SolidColorBrush.Parse("#e0a943"),
            BadgeBackground = SolidColorBrush.Parse("#1f1a10"),
            BadgeBorder = SolidColorBrush.Parse("#3b2f16"),
            BadgeForeground = SolidColorBrush.Parse("#e0a943"),
            ActionForeground = SolidColorBrush.Parse("#58a6f0"),
        },
        new WorkspaceViewModel
        {
            Name = "Tinkering sandbox",
            Path = "C:/Users/you/Documents/wb-sandbox",
            Badge = "LOCAL",
            Action = "switch →",
            IsCurrent = false,
            Dot = SolidColorBrush.Parse("#5c6772"),
            BadgeBackground = SolidColorBrush.Parse("#171b20"),
            BadgeBorder = SolidColorBrush.Parse("#272d34"),
            BadgeForeground = SolidColorBrush.Parse("#8b959e"),
            ActionForeground = SolidColorBrush.Parse("#58a6f0"),
        },
    ];

    [RelayCommand]
    private void ToggleWorkspaces() => WorkspacesOpen = !WorkspacesOpen;

    [RelayCommand]
    private void CloseWorkspaces() => WorkspacesOpen = false;
}
