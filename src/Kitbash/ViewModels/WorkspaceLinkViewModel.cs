using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>
/// One row in the workspace links section. The name is the whole row, since a workspace
/// names its own links and the address would only repeat what the label already says.
/// </summary>
public partial class WorkspaceLinkViewModel : ObservableObject
{
    private readonly WorkspaceLink _link;
    private readonly Action<WorkspaceLink> _open;

    public WorkspaceLinkViewModel(WorkspaceLink link, Action<WorkspaceLink> open)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(open);

        _link = link;
        _open = open;
    }

    public string Label => _link.Label;

    public IconGlyph Icon => _link.Icon;

    /// <summary>The address, which the row shows on hover rather than in the row.</summary>
    public string Address => _link.Address.ToString();

    [RelayCommand]
    private void Open() => _open(_link);
}
