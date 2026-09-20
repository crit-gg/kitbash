using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// One workspace on the workspaces page, with everything needed to open it without
/// switching to it first. The engine, the openers and the tools are each read for this
/// workspace alone.
/// </summary>
public sealed partial class WorkspaceRowViewModel : ViewModelBase
{
    /// <summary>
    /// The engine this workspace asks for. Replaced whole once the read comes back, so it
    /// is never half a state.
    /// </summary>
    [ObservableProperty]
    private EngineViewModel _engine = EngineViewModel.Reading;

    /// <summary>The tools this workspace provides, which the row's tools menu lists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTools))]
    private IReadOnlyList<InstalledTool> _tools = [];

    public WorkspaceRowViewModel(WorkspaceViewModel workspace, OpenInViewModel openIn)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(openIn);

        Workspace = workspace;
        OpenIn = openIn;
    }

    /// <summary>The switcher's own row, which is where the name, the path and the badge are.</summary>
    public WorkspaceViewModel Workspace { get; }

    /// <summary>What this workspace can be opened in, gathered when the page opened.</summary>
    public OpenInViewModel OpenIn { get; }

    public string Name => Workspace.Name;

    public string Path => Workspace.Path;

    /// <summary>The folder this row is about, which every action here is given.</summary>
    public string Root => Workspace.Workspace.Root;

    public bool IsLocal => Workspace.IsLocal;

    public bool IsMissing => Workspace.IsMissing;

    public bool HasBadge => Workspace.HasBadge;

    public string Badge => Workspace.Badge;

    /// <summary>False leaves the tools button out, the way an empty Open in menu goes.</summary>
    public bool HasTools => Tools.Count > 0;
}
