using Avalonia.Controls;
using Humanizer;
using Kitbash.Ui.Controls;
using Kitbash.Updates;

namespace Kitbash.Views;

/// <summary>
/// Reports the launcher replacing itself. It is not an offer, so it carries no button
/// and the close glyph is the only way past it.
/// </summary>
public partial class UpdateDialog : DialogWindow
{
    private readonly CancellationTokenSource _cancellation = new();

    private string _version = string.Empty;
    private string _size = string.Empty;

    public UpdateDialog()
    {
        InitializeComponent();

        // It runs before the launcher exists, so there is no owner to centre on and
        // nothing behind it in the task bar to say what is happening.
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
    }

    /// <summary>
    /// Downloads, then applies and restarts. This is the only thing on screen while it
    /// runs, since the launcher does not open until the update is settled one way or the
    /// other. Anything that goes wrong takes the dialog down and returns, and the copy
    /// already on the machine opens as it was.
    /// </summary>
    public static async Task RunAsync(IApplicationUpdates updates, AvailableUpdate found)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(found);

        var dialog = new UpdateDialog();
        dialog.Describe(found);

        // Progress captures this thread's context when it is built here, so every report
        // arrives back on the UI thread and Report touches bound state directly.
        var progress = new Progress<int>(dialog.Report);

        Task? running = null;

        async Task Work()
        {
            try
            {
                await updates.DownloadAsync(found, progress, dialog._cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // The close glyph did this and the window is already going.
                return;
            }
            catch (Exception)
            {
                // The service has already said what went wrong. Nothing has changed, so
                // the dialog goes and the launcher carries on as it was.
                dialog.Close();

                return;
            }

            dialog.Finish();

            try
            {
                // Does not return when it works. The process is replaced by the new copy.
                updates.ApplyAndRestart(found);
            }
            catch (Exception)
            {
                // Said by the service too.
            }

            dialog.Close();
        }

        // The work starts once the dialog is up, so a fast download cannot close a window
        // that has not opened.
        dialog.Opened += (_, _) => running = Work();

        // Shown rather than opened as a dialog, because there is no owner yet. Closed is
        // what ShowDialog would have awaited.
        var closed = new TaskCompletionSource();
        dialog.Closed += (_, _) => closed.TrySetResult();

        dialog.Show();

        await closed.Task;

        // Awaited after the window has gone, so the token is not disposed under work that
        // is still winding down.
        if (running is not null)
        {
            await running;
        }

        dialog._cancellation.Dispose();
    }

    /// <summary>What is coming, before anything has been fetched.</summary>
    public void Describe(AvailableUpdate found)
    {
        ArgumentNullException.ThrowIfNull(found);

        _version = found.Version;

        // The feed states the size, so this is the real one rather than a total worked
        // out from what has arrived so far.
        _size = found.Size.Bytes().Humanize("0.#");

        Report(0);
    }

    /// <summary>One report from the download, on the UI thread.</summary>
    public void Report(int percent)
    {
        var done = Math.Clamp(percent, 0, 100);

        Meter.Value = done;

        // Velopack counts the download alone, and the checksum runs inside the same call,
        // so everything after this reaches a hundred is the check rather than the fetch.
        Stage.Text = done < 100
            ? $"Downloading Kitbash {_version}"
            : "Checking the download";

        // A percentage of a stated total, because Velopack reports whole percentages and
        // nothing else. Bytes so far would be that number multiplied back up, which is a
        // figure this code does not have.
        Detail.Text = $"{done}% of {_size}";
    }

    /// <summary>
    /// The download is in. Nothing here can be cancelled any more, so the bar sits full
    /// while the swap happens.
    /// </summary>
    public void Finish()
    {
        Stage.Text = $"Restarting on {_version}";
        Detail.Text = string.Empty;
        Meter.Value = 100;
    }

    /// <summary>
    /// Closing by any route cancels, which is the only thing this dialog offers. So the
    /// close glyph and Escape need no wiring of their own.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _cancellation.Cancel();
    }
}
