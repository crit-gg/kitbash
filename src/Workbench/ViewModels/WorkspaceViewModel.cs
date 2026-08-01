using Workbench.Core.Workspaces;

namespace Workbench.ViewModels;

/// <summary>One workspace in the switcher. Everything shown is derived from the model.</summary>
/// <remarks>
/// This carries no brushes. It reports what is true and the view turns that into
/// classes, so every colour comes from the theme and a workspace row can be restyled
/// without touching a view model.
/// </remarks>
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

    public bool IsMissing => Workspace.IsMissing;

    public bool IsLocal => Workspace.IsLocal;

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
}
