using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;

namespace Kitbash.Views;

/// <summary>
/// Asks before a repository a link named joins the global list. Adding one is how a
/// program gets onto this machine, so a link never adds one on its own.
/// </summary>
public partial class AddRepositoryDialog : DialogWindow
{
    public AddRepositoryDialog()
    {
        InitializeComponent();
    }

    public static AddRepositoryDialog For(WebAddress url)
    {
        var dialog = new AddRepositoryDialog();

        dialog.Heading.Text = $"Add {url.Value.Host} as a tool repository?";
        dialog.Address.Text = url.ToString();

        return dialog;
    }
}
