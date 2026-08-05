using Avalonia.Controls;
using Kitbash.Core.Godot;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Asks before something installed is removed. A real window, so the desktop owns
/// modality and there is no scrim.
/// </summary>
public partial class UninstallDialog : DialogWindow
{
    public UninstallDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The consequence line, which differs by kind. An engine Kitbash installed is
    /// deleted. An imported one is forgotten and its files are left alone.
    /// </summary>
    public static UninstallDialog For(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var dialog = new UninstallDialog();
        var name = EngineRowViewModel.NameOf(engine.Tag);

        dialog.Titled("Uninstall engine");

        dialog.Heading.Text = engine.IsImported
            ? $"Remove Godot {name} from the list?"
            : $"Uninstall Godot {name}?";

        dialog.Body.Text = engine.IsImported
            ? "The files stay where they are, since Kitbash did not put them there. "
              + "You can add the folder again at any time."
            : "The files are deleted from disk. You can install this build again at any time.";

        dialog.Confirm.Content = engine.IsImported ? "Remove" : "Uninstall";
        dialog.Path.Text = engine.Directory;
        dialog.Size.Text = engine.IsMissing
            ? "the folder is gone"
            : $"{EngineRowViewModel.Size(engine.SizeOnDisk)} on disk";

        return dialog;
    }

    /// <summary>
    /// The tool's version directories go and everything a person owns stays, so the
    /// consequence line says so rather than warning about losing work.
    /// </summary>
    /// <param name="onDisk">What the versions take up, already written for a person.</param>
    public static UninstallDialog For(InstalledTool tool, string onDisk)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var dialog = new UninstallDialog();

        dialog.Titled("Uninstall tool");

        dialog.Heading.Text = $"Uninstall {tool.Name}?";
        dialog.Body.Text = "The files are deleted from disk. Its settings and anything it "
            + "wrote in a workspace are left alone, so installing it again finds them.";

        dialog.Confirm.Content = "Uninstall";
        dialog.Path.Text = tool.Directory;
        dialog.Size.Text = onDisk;

        return dialog;
    }

    private void Titled(string title)
    {
        Title = title;
        Bar.Title = title;
    }
}
