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

    /// <summary>The same question for an engine repository, which a workspace names by <paramref name="name"/>.</summary>
    public static AddRepositoryDialog ForEngines(WebAddress url, string name)
    {
        var dialog = new AddRepositoryDialog();

        dialog.Title = "Add engine repository";
        dialog.Bar.Title = "Add engine repository";
        dialog.Heading.Text = $"Add {name} as an engine repository?";
        dialog.Body.Text =
            "A link asked for this. Kitbash will offer the Godot builds it publishes, and a "
            + "workspace naming it installs them, so add it only if you trust whoever owns it.";
        dialog.Primary.Content = "Add repository";
        dialog.Address.Text = url.ToString();

        return dialog;
    }
}
