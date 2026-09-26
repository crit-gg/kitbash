using Avalonia.Controls;
using Kitbash.Core.Godot;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Reports building and importing while a project opens.
/// </summary>
public partial class LaunchDialog : DialogWindow
{
    private readonly CancellationTokenSource _cancellation = new();

    private GodotLaunchMode _mode;
    private bool _openWhenDone;

    public LaunchDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Runs the work with this dialog over it, and takes it down when the work ends. The
    /// work starts once the dialog is up, so something quick cannot close a window that
    /// has not opened.
    /// </summary>
    /// <returns>
    /// How it ended. <c>OpenEditor</c> is the person asking to open the editor from the
    /// finished dialog, which only a rebuild can offer.
    /// </returns>
    public static async Task<GodotLaunchOutcome> RunAsync(
        Window owner,
        GodotProject project,
        GodotLaunchMode mode,
        Func<IProgress<GodotLaunchStep>, CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(work);

        var name = project.Name.Length > 0
            ? project.Name
            : System.IO.Path.GetFileName(project.Directory);

        var verb = mode switch
        {
            GodotLaunchMode.Editor => "Opening",
            GodotLaunchMode.Rebuild => "Rebuilding",
            _ => "Starting",
        };

        var dialog = new LaunchDialog { Title = $"{verb} {name}", _mode = mode };

        // A rebuild has already deleted something by the time it can be cancelled, so it
        // says what that leaves rather than the note the other two modes carry.
        if (mode == GodotLaunchMode.Rebuild)
        {
            dialog.Note.Text =
                "Cancelling stops the step that is running. The cache is made again the "
                + "next time the project opens.";
        }

        dialog.Report(new GodotLaunchStep(GodotLaunchStage.Checking));

        // Progress captures this thread's context when it is built here, so every report
        // arrives back on the UI thread and Report touches bound state directly.
        var progress = new Progress<GodotLaunchStep>(dialog.Report);

        Exception? failure = null;
        Task? running = null;

        async Task Work()
        {
            try
            {
                await work(progress, dialog._cancellation.Token);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // A rebuild that worked has something to say and something to offer, so the
            // window stays up. Everything else has already gone where it was going.
            if (failure is null && mode == GodotLaunchMode.Rebuild)
            {
                dialog.Finish();

                return;
            }

            dialog.Close();
        }

        dialog.Opened += (_, _) => running = Work();

        await dialog.ShowDialog(owner);

        // Awaited after the window has gone, so the token is not disposed under work that
        // is still winding down. Cancelling kills a process tree and that is not instant.
        if (running is not null)
        {
            await running;
        }

        dialog._cancellation.Dispose();

        if (failure is not null and not OperationCanceledException)
        {
            throw failure;
        }

        if (dialog._openWhenDone)
        {
            return GodotLaunchOutcome.OpenEditor;
        }

        // The only failure that reaches here is the cancellation, since every other one
        // was thrown a line ago.
        return failure is null ? GodotLaunchOutcome.Finished : GodotLaunchOutcome.Cancelled;
    }

    /// <summary>
    /// The rebuild worked. The bar and the Cancel go, and the two answers arrive.
    /// </summary>
    public void Finish()
    {
        Stage.Text = "Rebuild complete";

        // The detail line is mono and carries values, so the sentence goes in the note,
        // which is the one line here written in the UI family.
        Detail.Text = string.Empty;
        Note.Text = "The import cache was deleted and made again. Nothing has been started.";
        Meter.IsVisible = false;

        CancelButton.IsVisible = false;
        DonePanel.IsVisible = true;

        DismissButton.Click += (_, _) => Close(false);

        OpenButton.Click += (_, _) =>
        {
            _openWhenDone = true;
            Close(true);
        };

        // Focus arrives as though tabbed to, so the button that answers wears the halo,
        // which is the rule the dialog base already follows for a roled one.
        OpenButton.Focus(Avalonia.Input.NavigationMethod.Tab);
    }

    /// <summary>One step from the launcher, on the UI thread.</summary>
    public void Report(GodotLaunchStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        Stage.Text = step.Stage switch
        {
            GodotLaunchStage.Updating => "Updating the engine",
            GodotLaunchStage.Cleaning => "Deleting the import cache",
            GodotLaunchStage.Building => "Building C#",
            GodotLaunchStage.Scanning => "Scanning assets",
            GodotLaunchStage.Importing => "Importing assets",
            GodotLaunchStage.Starting => _mode == GodotLaunchMode.Editor
                ? "Starting the editor"
                : "Starting the project",
            _ => "Checking what this project needs",
        };

        Detail.Text = step.Detail;

        // A real number when the work reports one, and a spinner when it does not. Godot
        // reports its own import progress and neither a build nor a start reports any.
        Meter.IsIndeterminate = step.Fraction is null;

        if (step.Fraction is { } part)
        {
            Meter.Value = Math.Clamp(part, 0, 1) * 100;
        }

        // Nothing can be cancelled once it has been handed to the desktop.
        CancelButton.IsEnabled = step.Stage != GodotLaunchStage.Starting;
    }

    /// <summary>
    /// Closing by any route cancels, which is the same thing Cancel does. So the close
    /// glyph and Escape need no wiring of their own.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _cancellation.Cancel();
    }
}
