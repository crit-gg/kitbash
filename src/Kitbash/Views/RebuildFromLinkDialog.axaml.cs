using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>Asks before a link's clean rebuild runs, and which workspace when several match.</summary>
public partial class RebuildFromLinkDialog : DialogWindow
{
    public RebuildFromLinkDialog()
    {
        InitializeComponent();
    }

    public RebuildFromLinkViewModel? Model => DataContext as RebuildFromLinkViewModel;
}
