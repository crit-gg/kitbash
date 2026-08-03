using Avalonia.Controls;
using Avalonia.Input.Platform;
using Workbench.Core.Godot;
using Workbench.Ui.Controls;

namespace Workbench.Views;

/// <summary>
/// Says a build or an import failed, and shows what the program wrote.
/// </summary>
/// <remarks>
/// <para>
/// The design's Error kind. **The output is the whole point.** A C# build that fails is
/// only useful with its compiler errors, and there is nowhere else a person could go and
/// read them, since Workbench started the process and owns its pipes.
/// </para>
/// <para>
/// It has no Retry, which the design's example does. Retrying means pressing Open again,
/// and after a failed build that is almost never the next thing to do. Fixing the code is.
/// </para>
/// </remarks>
public partial class LaunchFailedDialog : DialogWindow
{
    public LaunchFailedDialog()
    {
        InitializeComponent();
    }

    public static async Task Show(Window owner, GodotLaunchException failure)
    {
        ArgumentNullException.ThrowIfNull(owner);

        await For(failure).ShowDialog(owner);
    }

    /// <summary>
    /// The dialog, built and filled in but not shown. Separate from <see cref="Show"/> so
    /// it can be rendered without a window to own it.
    /// </summary>
    public static LaunchFailedDialog For(GodotLaunchException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var dialog = new LaunchFailedDialog();

        var what = failure.Stage switch
        {
            GodotLaunchStage.Building => "The C# build failed",
            GodotLaunchStage.Importing => "Importing assets failed",
            _ => "The editor did not start",
        };

        // The title names the task and the heading names what went wrong, which is the
        // design's rule that a heading never repeats the title.
        dialog.Heading.Text = $"{what}.";

        dialog.Body.Text = failure.Stage switch
        {
            GodotLaunchStage.Building =>
                "The editor was not started, since it would have opened without the "
                + "assemblies this project needs.",
            GodotLaunchStage.Importing =>
                "The editor was not started. Opening the project in Godot will import "
                + "again and show the same problem.",
            _ => failure.Message,
        };

        var output = failure.Output.Length > 0 ? failure.Output : failure.Message;

        dialog.Output.Text = output;
        dialog.OutputWell.IsVisible = output.Length > 0;

        dialog.Copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(output);
            }
        };

        return dialog;
    }
}
