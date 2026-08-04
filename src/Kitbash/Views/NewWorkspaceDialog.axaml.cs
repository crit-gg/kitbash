using Kitbash.Core.Workspaces;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Describes a workspace and hands it back. Nothing is written here, so cancelling leaves
/// no folder and no repository behind.
/// </summary>
public partial class NewWorkspaceDialog : DialogWindow
{
    public NewWorkspaceDialog()
    {
        InitializeComponent();
    }

    /// <summary>What was described. Only worth reading once the dialog answered yes.</summary>
    public NewWorkspace? Request => Model?.Request;

    /// <summary>Whether the engine picked has to be installed before the project is made.</summary>
    public bool NeedsInstall => Model?.NeedsInstall ?? false;

    private NewWorkspaceViewModel? Model => DataContext as NewWorkspaceViewModel;

    // The engine list reads a disk and a network, so the window opens on the empty list
    // and fills in when the read lands.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Model is { } model)
        {
            _ = model.LoadEnginesAsync();
        }
    }
}
