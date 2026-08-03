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
/// **There is no Retry and no Open anyway.** A build that failed means the editor would
/// open without the assemblies the project needs, so opening it is the one thing this
/// dialog must not offer. The only ways on are fixing the code or turning the build off
/// in settings, and both are deliberate acts elsewhere.
/// </para>
/// </remarks>
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
        // than always naming the editor.
        var target = mode == GodotLaunchMode.Editor ? "The editor" : "The project";

        var dialog = new LaunchFailedDialog();

        var what = failure.Stage switch
        {
            GodotLaunchStage.Building => "The C# build failed",
            GodotLaunchStage.Importing => "Importing assets failed",
            _ => mode == GodotLaunchMode.Editor
                ? "The editor did not start"
                : "The project did not start",
        };

        // The title names the task and the heading names what went wrong, which is the
        // design's rule that a heading never repeats the title.
        dialog.Heading.Text = $"{what}.";

        dialog.Body.Text = failure.Stage switch
        {
            GodotLaunchStage.Building =>
                $"{target} was not started, since it would have run without the "
                + "assemblies this project needs.",
            GodotLaunchStage.Importing =>
                $"{target} was not started. Opening the project in Godot will import "
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
            _ => "Copy details",
        };

        dialog.Copy.Content = label;
        dialog.Copy.Click += async (_, _) => await dialog.CopyAsync(output, label);

        return dialog;
    }

    /// <summary>
    /// Puts the whole log on the clipboard and says so on the button for a moment.
    /// </summary>
    /// <remarks>
    /// **The whole log, not what the well is showing.** The well shows the errors, and the
    /// log is what somebody pastes into a message or a search, so the two are deliberately
    /// different and only the button can say which one was taken.
    ///
    /// The confirmation is the button's own label rather than a toast. A toast belongs to
    /// the window that raised it, and this is a modal in front of that window, so one
    /// would appear behind the thing being read.
    /// </remarks>
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
