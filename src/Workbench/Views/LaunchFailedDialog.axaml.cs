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
        var shown = Problems(output);

        dialog.Output.Text = shown;
        dialog.OutputWell.IsVisible = shown.Length > 0;

        // Copy takes the whole thing, since the point of copying is to paste it somewhere
        // that can read all of it.
        dialog.Copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(output);
            }
        };

        return dialog;
    }

    /// <summary>
    /// The lines worth reading out of a program's output, or all of it when none stand out.
    /// </summary>
    /// <remarks>
    /// **A build log is not evidence, the errors in it are.** Measured on a two error
    /// build: dotnet writes 13 lines, opens with restore chatter, and prints every error
    /// twice, once where it happened and once in its summary. Showing that raw puts the
    /// answer on line three and repeats it on line seven.
    ///
    /// Both formats are matched. MSBuild writes <c>path(3,28): error CS0103: ...</c> and
    /// Godot writes lines beginning ERROR. Counting lines such as "2 Error(s)" are not
    /// errors and do not match either.
    /// </remarks>
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
