using Avalonia.Controls;
using Avalonia.Threading;
using Kitbash.Tools;
using Kitbash.Ui.Controls;

namespace Kitbash.Views;

/// <summary>
/// Reports a script while it runs, out of the lines the script itself writes.
/// </summary>
public partial class ToolRunDialog : DialogWindow
{
    /// <summary>How many lines the log keeps. A script that writes more loses the oldest.</summary>
    private const int KeptLines = 500;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly Queue<string> _lines = new();

    private bool _refreshing;

    /// <summary>
    /// The generated InitializeComponent, since that is what assigns the named fields.
    /// </summary>
    public ToolRunDialog()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close(false);
        Log.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty)
            {
                Refresh();
            }
        };
    }

    /// <summary>
    /// Runs the script with this dialog over it. The work starts once the dialog is up, so
    /// something quick cannot close a window that has not opened.
    /// </summary>
    /// <param name="subject">The tool's name, for the line a person reads.</param>
    /// <returns>How it ended, or null when it was cancelled.</returns>
    public static async Task<ToolRunOutcome?> RunAsync(
        Window owner,
        string subject,
        Func<IProgress<ToolProgressStep>, CancellationToken, Task<ToolRunOutcome>> work)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(work);

        var dialog = new ToolRunDialog { Title = $"Running {subject}" };

        dialog.Stage.Text = $"Running {subject}";

        // Progress captures this thread's context when it is built here, so every report
        // arrives back on the UI thread and Report touches bound state directly.
        var progress = new Progress<ToolProgressStep>(dialog.Report);

        Exception? failure = null;
        ToolRunOutcome? outcome = null;
        Task? running = null;

        async Task Work()
        {
            try
            {
                outcome = await work(progress, dialog._cancellation.Token);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // A run that failed has something to say and its log is the only place it says
            // it, so the window stays up. Anything else has finished.
            if (failure is null && outcome is { Worked: false })
            {
                dialog.Finish(subject, outcome.ExitCode);

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

        return failure is null ? outcome : null;
    }

    /// <summary>One line the script wrote, on the UI thread.</summary>
    public void Report(ToolProgressStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Stage is { } stage)
        {
            Stage.Text = stage;
        }

        if (step.Detail is { } detail)
        {
            Detail.Text = detail;
        }

        if (step.Progress is { } part)
        {
            Meter.IsIndeterminate = false;
            Meter.Value = part;
        }
        else if (step.IsIndeterminate)
        {
            Meter.IsIndeterminate = true;
        }

        if (step.Log is { } line)
        {
            Append(line);
        }
    }

    /// <summary>
    /// The script ended badly. The bar and Cancel go, the log opens, and the one button
    /// left closes the window.
    /// </summary>
    public void Finish(string subject, int exitCode)
    {
        Stage.Text = $"{subject} did not finish";
        Detail.Text = string.Empty;
        Meter.IsVisible = false;
        Note.IsVisible = false;

        Failure.Title = $"The script stopped with code {exitCode}.";
        Failure.IsVisible = true;

        Log.IsVisible = true;
        Log.IsExpanded = true;

        CancelButton.IsVisible = false;
        CloseButton.IsVisible = true;

        // Focus arrives as though tabbed to, so the button that answers wears the halo.
        CloseButton.Focus(Avalonia.Input.NavigationMethod.Tab);
    }

    /// <summary>
    /// Keeps the line and asks for one redraw. A script can write faster than the screen
    /// can follow, so the text is rebuilt when the thread is next idle rather than per line.
    /// </summary>
    private void Append(string line)
    {
        _lines.Enqueue(line);

        while (_lines.Count > KeptLines)
        {
            _lines.Dequeue();
        }

        Log.IsVisible = true;

        if (_refreshing || !Log.IsExpanded)
        {
            return;
        }

        _refreshing = true;
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Background);
    }

    private void Refresh()
    {
        _refreshing = false;

        if (!Log.IsExpanded)
        {
            return;
        }

        LogText.Text = string.Join(Environment.NewLine, _lines);
        LogScroll.ScrollToEnd();
    }

    /// <summary>
    /// Closing by any route cancels, which is the same thing Cancel does. A run that has
    /// already ended has nothing left to cancel.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _cancellation.Cancel();
    }
}
