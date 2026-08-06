using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

    /// <summary>How long the button says Copied before it goes back to its own word.</summary>
    private static readonly TimeSpan Confirmation = TimeSpan.FromMilliseconds(1400);

    private readonly CancellationTokenSource _cancellation = new();
    private readonly Queue<string> _lines = new();

    private bool _refreshing;
    private bool _copying;

    /// <summary>
    /// The generated InitializeComponent, since that is what assigns the named fields.
    /// </summary>
    public ToolRunDialog()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close(false);
        CopyButton.Click += async (_, _) => await CopyAsync();
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

            // A run with something to say keeps its window, since its log is the only
            // place it says it. A script that wrote nothing has nothing to read.
            if (failure is null && outcome is { } ended && (!ended.Worked || dialog.HasOutput))
            {
                dialog.Finish(subject, ended.ExitCode);

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

    /// <summary>Whether the script has written a line the log is holding.</summary>
    public bool HasOutput => _lines.Count > 0;

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
    /// The script ended and the window is being kept. The bar and Cancel go, the log
    /// opens, and the one button left closes the window.
    /// </summary>
    /// <param name="exitCode">Zero for a run that worked, which is kept only for its log.</param>
    public void Finish(string subject, int exitCode)
    {
        var worked = exitCode == 0;

        Stage.Text = worked ? $"{subject} finished" : $"{subject} did not finish";
        Detail.Text = string.Empty;
        Meter.IsVisible = false;
        Note.IsVisible = false;

        if (!worked)
        {
            Failure.Title = $"The script stopped with code {exitCode}.";
            Failure.IsVisible = true;
        }

        // A failure opens the log whether or not it holds a line, since the code is all
        // it otherwise says. A run that worked is only kept for its lines.
        Log.IsVisible = !worked || HasOutput;
        Log.IsExpanded = Log.IsVisible;

        CancelButton.IsVisible = false;
        CloseButton.IsVisible = true;

        // Focus arrives as though tabbed to, so the button that answers wears the halo.
        CloseButton.Focus(Avalonia.Input.NavigationMethod.Tab);
    }

    /// <summary>
    /// Puts the whole log on the clipboard and says so on the button for a moment. What
    /// lands is what the log holds, so a script that wrote past the limit is short of its
    /// oldest lines here too.
    /// </summary>
    private async Task CopyAsync()
    {
        if (_copying || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        _copying = true;

        try
        {
            // Avalonia 12 replaced SetTextAsync with a format and a value.
            await clipboard.SetValueAsync(DataFormat.Text, string.Join(Environment.NewLine, _lines));

            CopyButton.Content = "Copied";

            await Task.Delay(Confirmation);

            CopyButton.Content = "Copy details";
        }
        finally
        {
            _copying = false;
        }
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
        CopyButton.IsVisible = true;

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

        // The newest line is what is followed, so the offset moves down its own axis
        // alone. ScrollToEnd also goes hard left, which takes back a sideways scroll
        // every time the script writes.
        LogScroll.Offset = LogScroll.Offset.WithY(double.PositiveInfinity);
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
