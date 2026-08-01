using Avalonia.Media;
using Workbench.Core.Workspaces;

namespace Workbench.ViewModels;

/// <summary>One workspace in the switcher. Everything shown is derived from the model.</summary>
public sealed class WorkspaceViewModel
{
    public WorkspaceViewModel(Workspace workspace, bool isCurrent, string displayPath)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        Workspace = workspace;
        IsCurrent = isCurrent;
        Path = displayPath;
    }

    public Workspace Workspace { get; }

    public bool IsCurrent { get; }

    public string Name => Workspace.Name;

    /// <summary>Shortened for display. Use <see cref="Workspace"/> for the real root.</summary>
    public string Path { get; }

    /// <summary>A workspace in good order carries no badge.</summary>
    public bool HasBadge => Workspace.IsMissing || Workspace.IsLocal;

    public string Badge => Workspace.IsMissing ? "MISSING" : "LOCAL";

    public string Action => (IsCurrent, Workspace.IsMissing) switch
    {
        (true, _) => "current",
        (_, true) => "not found",
        _ => "switch",
    };

    public bool CanSwitch => !IsCurrent && !Workspace.IsMissing;

    public IBrush Dot => Workspace switch
    {
        { IsMissing: true } => Brush("#ea5257"),
        { IsLocal: true } => Brush("#5c6772"),
        _ => Brush("#52cfa5"),
    };

    public IBrush BadgeBackground => Workspace.IsMissing ? Brush("#1e1416") : Brush("#171b20");

    public IBrush BadgeBorder => Workspace.IsMissing ? Brush("#3a1e21") : Brush("#272d34");

    public IBrush BadgeForeground => Workspace.IsMissing ? Brush("#ea8a8a") : Brush("#8b959e");

    public IBrush ActionForeground => (IsCurrent, Workspace.IsMissing) switch
    {
        (true, _) => Brush("#6e7982"),
        (_, true) => Brush("#49535b"),
        _ => Brush("#58a6f0"),
    };

    /// <summary>The current workspace carries an accent edge.</summary>
    public IBrush EdgeMark => IsCurrent ? Brush("#58a6f0") : Brushes.Transparent;

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);
}
