using Avalonia.Controls;
using Workbench.Core.Godot;
using Workbench.Ui.Controls;

namespace Workbench.Views;

/// <summary>
/// Reports building and importing while a project opens.
/// </summary>
/// <remarks>
/// <para>
/// The design's Progress kind. A line saying what is happening, a mono line under it, a
/// bar, and what cancelling costs. There is no primary button while work runs.
/// </para>
/// <para>
/// **Two departures, both because there is no number to show.** The bar is indeterminate,
/// since neither a C# build nor a Godot import reports how far through it is, and the
/// mono line carries which program is running rather than a count. Filling a bar at its
/// own pace would be inventing the progress.
/// </para>
/// <para>
/// **The close glyph stays live rather than flattening to disabled.** The design disables
/// it, and here closing and Cancel do exactly the same thing, so a dead glyph would refuse
/// a gesture the dialog already honours. The window keeps its shape either way, which is
/// what that rule is protecting.
/// </para>
/// </remarks>
public partial class LaunchDialog : DialogWindow
{
    private readonly CancellationTokenSource _cancellation = new();

    public LaunchDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Runs the work with this dialog over it, and takes it down when the work ends. The
    /// work starts once the dialog is up, so something quick cannot close a window that
    /// has not opened.
    /// </summary>
    /// <remarks>
    /// **The result is the work's, not the dialog's.** A dialog answers accept or cancel
    /// and neither is what a caller wants here. Cancelling closes the window, closing
    /// cancels the token, the token ends the work, and what comes back is what the caller
    /// reports. Cancelling is an answer rather than a fault, so it is not thrown on.
    /// </remarks>
    public static async Task RunAsync(
        Window owner,
        GodotProject project,
        Func<IProgress<GodotLaunchStep>, CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(work);

        var name = project.Name.Length > 0
            ? project.Name
            : System.IO.Path.GetFileName(project.Directory);

        var dialog = new LaunchDialog { Title = $"Opening {name}" };

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
    }

    /// <summary>One step from the launcher, on the UI thread.</summary>
    public void Report(GodotLaunchStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        Stage.Text = step.Stage switch
        {
            GodotLaunchStage.Building => "Building C#",
            GodotLaunchStage.Importing => "Importing assets",
            GodotLaunchStage.Starting => "Starting the editor",
            _ => "Checking what this project needs",
        };

        Detail.Text = step.Detail;

        // Nothing can be cancelled once the editor has been handed to the desktop.
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
