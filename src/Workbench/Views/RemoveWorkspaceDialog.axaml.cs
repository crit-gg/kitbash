using Workbench.Ui.Controls;
using Workbench.ViewModels;

namespace Workbench.Views;

/// <summary>Asks before a workspace leaves the list. Nothing on disk is touched.</summary>
public partial class RemoveWorkspaceDialog : DialogWindow
{
    public RemoveWorkspaceDialog()
    {
        InitializeComponent();
    }

    public static RemoveWorkspaceDialog For(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var dialog = new RemoveWorkspaceDialog();

        dialog.Heading.Text = $"Remove {workspace.Name} from the list?";
        dialog.Path.Text = workspace.Workspace.Root;

        return dialog;
    }
}
