using Avalonia.Controls;
using Workbench.Core.Godot;
using Workbench.Ui.Controls;
using Workbench.ViewModels;

namespace Workbench.Views;

/// <summary>
/// Asks before an engine is removed. A real window, so the desktop owns modality and there
/// is no scrim.
/// </summary>
public partial class UninstallDialog : DialogWindow
{
    public UninstallDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// **The consequence line has two forms, and which one shows is the whole point.** An
    /// engine Workbench installed is deleted. One a person pointed at is forgotten and its
    /// files are left alone, since Workbench did not put them there.
    /// </summary>
    /// <param name="path">
    /// The install path, already written for the width of the well. A path is shortened
    /// wherever it is shown, so the middle goes and the folder's own name survives, which
    /// trimming the end would take away.
    /// </param>
    public static UninstallDialog For(InstalledEngine engine, string path)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var dialog = new UninstallDialog();
        var name = EngineRowViewModel.NameOf(engine.Tag);

        dialog.Heading.Text = engine.IsImported
            ? $"Remove Godot {name} from the list?"
            : $"Uninstall Godot {name}?";

        dialog.Body.Text = engine.IsImported
            ? "The files stay where they are, since Workbench did not put them there. "
              + "You can add the folder again at any time."
            : "The files are deleted from disk. You can install this build again at any time.";

        dialog.Confirm.Content = engine.IsImported ? "Remove" : "Uninstall";
        dialog.Path.Text = path;
        dialog.Size.Text = engine.IsMissing
            ? "the folder is gone"
            : $"{EngineRowViewModel.Size(engine.SizeOnDisk)} on disk";

        return dialog;
    }
}
