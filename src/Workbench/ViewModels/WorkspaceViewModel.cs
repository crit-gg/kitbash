using Avalonia.Media;

namespace Workbench.ViewModels;

/// <summary>One workspace in the switcher.</summary>
public sealed class WorkspaceViewModel
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Badge { get; init; }

    public required string Action { get; init; }

    public required bool IsCurrent { get; init; }

    public required IBrush Dot { get; init; }

    public required IBrush BadgeBackground { get; init; }

    public required IBrush BadgeBorder { get; init; }

    public required IBrush BadgeForeground { get; init; }

    public required IBrush ActionForeground { get; init; }

    /// <summary>The current workspace carries a tinted row and an accent edge.</summary>
    public IBrush RowBackground => IsCurrent ? SolidColorBrush.Parse("#16212c") : Brushes.Transparent;

    public IBrush EdgeMark => IsCurrent ? SolidColorBrush.Parse("#58a6f0") : Brushes.Transparent;
}
