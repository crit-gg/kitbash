using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>Takes a new name for a workspace. The name belongs to this person alone.</summary>
public partial class RenameWorkspaceDialog : DialogWindow
{
    public RenameWorkspaceDialog()
    {
        InitializeComponent();
    }

    /// <summary>What was typed. Blank means the workspace goes back to naming itself.</summary>
    public string ChosenName => Value.Text ?? string.Empty;

    public static RenameWorkspaceDialog For(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var dialog = new RenameWorkspaceDialog();

        dialog.Value.Text = workspace.Name;
        dialog.Value.SelectAll();
        dialog.Path.Text = workspace.Path;

        return dialog;
    }
}
