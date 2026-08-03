using Avalonia.Controls;
using Avalonia.Input.Platform;
using Workbench.Core.Godot;
using Workbench.Ui.Controls;

namespace Workbench.Views;

/// <summary>
/// Says a build or an import failed, and shows what the program wrote.
/// </summary>
public partial class LaunchFailedDialog : DialogWindow
{
    /// <summary>How long the button says Copied before it goes back to its own word.</summary>
    private static readonly TimeSpan Confirmation = TimeSpan.FromMilliseconds(1400);

    private bool _copying;

    public LaunchFailedDialog()
    {
        InitializeComponent();
    }

    public static async Task Show(Window owner, GodotLaunchException failure, GodotLaunchMode mode)
    {
        ArgumentNullException.ThrowIfNull(owner);

        await For(failure, mode).ShowDialog(owner);
    }

    /// <summary>
    /// The dialog, built and filled in but not shown. Separate from <see cref="Show"/> so
    /// it can be rendered without a window to own it.
    /// </summary>
    public static LaunchFailedDialog For(
        GodotLaunchException failure, GodotLaunchMode mode = GodotLaunchMode.Editor)
    {
        ArgumentNullException.ThrowIfNull(failure);

        // What did not start, so the sentence says the thing the person asked for rather
        // than always naming the editor. A rebuild starts nothing, so it says so.
        var target = mode switch
        {
            GodotLaunchMode.Editor => "The editor",
            GodotLaunchMode.Rebuild => "The rebuild",
            _ => "The project",
        };

        var dialog = new LaunchFailedDialog();

        var what = failure.Stage switch
        {
            GodotLaunchStage.Cleaning => "The import cache could not be deleted",
            GodotLaunchStage.Building => "The C# build failed",
            GodotLaunchStage.Importing => "Importing assets failed",
            _ => mode switch
            {
                GodotLaunchMode.Editor => "The editor did not start",
                GodotLaunchMode.Rebuild => "The rebuild did not finish",
                _ => "The project did not start",
            },
        };

        // The title names the task and the heading names what went wrong, which is the
        // design's rule that a heading never repeats the title.
        dialog.Title = mode == GodotLaunchMode.Rebuild
            ? "Could not rebuild the project"
            : "Could not open the project";

        dialog.Heading.Text = $"{what}.";

        dialog.Body.Text = failure.Stage switch
        {
            GodotLaunchStage.Cleaning =>
                "Nothing was deleted and nothing was rebuilt. Something is most likely "
                + "holding a file in it, such as an editor that is already open.",
            GodotLaunchStage.Building => mode == GodotLaunchMode.Rebuild
                ? "The import cache was deleted and the build that follows it failed, so "
                  + "the assets were not imported. Fixing the build and rebuilding again "
                  + "is what puts the project back."
                : $"{target} was not started, since it would have run without the "
                  + "assemblies this project needs.",
            GodotLaunchStage.Importing => mode == GodotLaunchMode.Rebuild
                ? "The import cache was deleted and could not be made again. Opening the "
                  + "project in Godot will import again and show the same problem."
                : $"{target} was not started. Opening the project in Godot will import "
                  + "again and show the same problem.",
            _ => failure.Message,
        };

        var output = failure.Output.Length > 0 ? failure.Output : failure.Message;
        var shown = Problems(output);

        dialog.Output.Text = shown;
        dialog.OutputWell.IsVisible = shown.Length > 0;

        // The button says what it copies, since the well is showing something shorter and
        // a person has to know the whole thing is what lands on the clipboard.
        var label = failure.Stage switch
        {
            GodotLaunchStage.Building => "Copy build log",
            GodotLaunchStage.Importing => "Copy import log",
            GodotLaunchStage.Cleaning => "Copy details",
            _ => "Copy details",
        };

        dialog.Copy.Content = label;
        dialog.Copy.Click += async (_, _) => await dialog.CopyAsync(output, label);

        return dialog;
    }

    /// <summary>
    /// Puts the whole log on the clipboard and says so on the button for a moment.
    /// </summary>
    private async Task CopyAsync(string log, string label)
    {
        if (_copying || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        _copying = true;

        try
        {
            await clipboard.SetTextAsync(log);

            Copy.Content = "Copied";

            await Task.Delay(Confirmation);

            Copy.Content = label;
        }
        finally
        {
            _copying = false;
        }
    }

    /// <summary>
    /// The lines worth reading out of a program's output, or all of it when none stand out.
    /// </summary>
    private static string Problems(string output)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();

            var isProblem = line.Contains(": error ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("ERROR:", StringComparison.Ordinal)
                || line.StartsWith("SCRIPT ERROR:", StringComparison.Ordinal);

            if (isProblem && seen.Add(line))
            {
                problems.Add(line);
            }
        }

        return problems.Count > 0 ? string.Join(Environment.NewLine, problems) : output;
    }
}
