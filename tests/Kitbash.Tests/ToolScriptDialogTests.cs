using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The two windows a script tool opens, drawn for real with no display behind them. The
/// form is where a person answers and the modal is where the script reports.
/// </summary>
public sealed class ToolScriptDialogTests
{
    /// <summary>Every kind draws its own editor and nothing else draws beside it.</summary>
    [AvaloniaFact]
    public void TheFormDrawsOneEditorPerKind()
    {
        var tool = FakeScriptTool.Built(
            Input("words", ToolInputKind.Text),
            Input("count", ToolInputKind.Integer),
            Input("dry", ToolInputKind.Boolean),
            Input("where", ToolInputKind.Folder),
            Input("mode", ToolInputKind.Choice) with
            {
                Choices = [new ToolInputChoice("fast", "Fast"), new ToolInputChoice("careful", "Careful")],
            });

        var (window, _) = Shown(tool);

        try
        {
            // A spinner and a path field are each built out of a text box of their own,
            // so the plain one is the only one belonging to no other control.
            Assert.Single(
                Visible<TextBox>(window),
                box => box.FindAncestorOfType<PathField>() is null
                    && box.FindAncestorOfType<NumericUpDown>() is null);
            Assert.Single(Visible<NumericUpDown>(window));
            Assert.Single(Visible<ToggleSwitch>(window));
            Assert.Single(Visible<PathField>(window));

            var combo = Assert.Single(Visible<ComboBox>(window));

            Assert.Equal(2, combo.ItemCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The button that runs the script cannot be pressed while an answer is missing, and
    /// typing one brings it back.
    /// </summary>
    [AvaloniaFact]
    public void RunIsOffWhileTheFormIsNotAnswered()
    {
        var tool = FakeScriptTool.Built(
            Input("words", ToolInputKind.Text) with { Required = true });

        var (window, model) = Shown(tool);

        try
        {
            var run = Assert.Single(
                window.GetVisualDescendants().OfType<Button>(),
                button => button.Content is "Run");

            Assert.False(run.IsEffectivelyEnabled);

            model.Rows[0].Text = "sheets";

            Assert.True(run.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A path field is what a folder input draws, and it holds what was typed.</summary>
    [AvaloniaFact]
    public void AFolderInputIsAPathField()
    {
        var tool = FakeScriptTool.Built(
            Input("where", ToolInputKind.Folder) with { Default = "/art" });

        var (window, model) = Shown(tool);

        try
        {
            var field = Assert.Single(Visible<PathField>(window));

            Assert.Equal(PathTarget.Folder, field.Target);
            Assert.Equal("/art", field.Path);

            field.Path = "/sheets";

            Assert.Equal("/sheets", model.Rows[0].Text);
            Assert.Equal(["/sheets"], model.Arguments());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>What the script says is what the modal says.</summary>
    [AvaloniaFact]
    public void TheModalDrawsWhatTheScriptReports()
    {
        var dialog = new ToolRunDialog { Title = "Running Sprites" };

        dialog.Show();

        try
        {
            var stage = Named<TextBlock>(dialog, "Stage");
            var detail = Named<TextBlock>(dialog, "Detail");
            var meter = Named<ProgressBar>(dialog, "Meter");

            Assert.True(meter.IsIndeterminate);

            dialog.Report(new ToolProgressStep { Stage = "Reading files" });
            dialog.Report(new ToolProgressStep { Detail = "12 / 30", Progress = 40 });

            Assert.Equal("Reading files", stage.Text);
            Assert.Equal("12 / 30", detail.Text);
            Assert.False(meter.IsIndeterminate);
            Assert.Equal(40, meter.Value);

            // A step that mentions neither leaves both where they were.
            dialog.Report(new ToolProgressStep { Log = "copied foo.png" });

            Assert.Equal("Reading files", stage.Text);
            Assert.Equal(40, meter.Value);

            // And a script that stops knowing how far it is gets the spinner back.
            dialog.Report(new ToolProgressStep { IsIndeterminate = true });

            Assert.True(meter.IsIndeterminate);
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// The log is hidden until there is one, and its text is written when it is opened,
    /// since a script can write faster than the screen can follow.
    /// </summary>
    [AvaloniaFact]
    public void TheLogAppearsWithTheFirstLineAndFillsInWhenItIsOpened()
    {
        var dialog = new ToolRunDialog();

        dialog.Show();

        try
        {
            var log = Named<Expander>(dialog, "Log");

            Assert.False(log.IsVisible);

            dialog.Report(new ToolProgressStep { Log = "copied foo.png" });

            Assert.True(log.IsVisible);

            log.IsExpanded = true;

            var text = Opened(dialog);

            Assert.Equal("copied foo.png", text.Text);

            dialog.Report(new ToolProgressStep { Log = "copied bar.png" });

            // The text is rebuilt when the thread is next idle rather than per line, so a
            // second line is there once the pending work has run.
            Pump(dialog);

            Assert.Contains("copied bar.png", text.Text ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// A run that failed keeps its window, since the log is the only place it says what
    /// happened. Cancel goes, because there is nothing left to cancel.
    /// </summary>
    [AvaloniaFact]
    public void AFailedRunKeepsItsWindowAndOpensItsLog()
    {
        var dialog = new ToolRunDialog();

        dialog.Show();

        try
        {
            dialog.Report(new ToolProgressStep { Log = "that did not work", IsError = true });
            dialog.Finish("Sprites", 3);

            Assert.True(dialog.IsVisible);
            Assert.Equal("Sprites did not finish", Named<TextBlock>(dialog, "Stage").Text);
            Assert.False(Named<ProgressBar>(dialog, "Meter").IsVisible);
            Assert.False(Named<Button>(dialog, "CancelButton").IsVisible);
            Assert.True(Named<Button>(dialog, "CloseButton").IsVisible);

            var alert = Named<Alert>(dialog, "Failure");

            Assert.True(alert.IsVisible);
            Assert.Contains("code 3", alert.Title ?? string.Empty, StringComparison.Ordinal);

            var log = Named<Expander>(dialog, "Log");

            Assert.True(log.IsExpanded);
            Assert.Equal("that did not work", Opened(dialog).Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// A run that worked and wrote something keeps its window too, so the lines can be
    /// read. Nothing is marked wrong about it.
    /// </summary>
    [AvaloniaFact]
    public void ARunThatWorkedKeepsItsWindowForItsOutput()
    {
        var dialog = new ToolRunDialog();

        dialog.Show();

        try
        {
            dialog.Report(new ToolProgressStep { Log = "copied foo.png" });
            dialog.Finish("Sprites", 0);

            Assert.True(dialog.IsVisible);
            Assert.Equal("Sprites finished", Named<TextBlock>(dialog, "Stage").Text);
            Assert.False(Named<ProgressBar>(dialog, "Meter").IsVisible);
            Assert.False(Named<Alert>(dialog, "Failure").IsVisible);
            Assert.False(Named<Button>(dialog, "CancelButton").IsVisible);
            Assert.True(Named<Button>(dialog, "CloseButton").IsVisible);

            var log = Named<Expander>(dialog, "Log");

            Assert.True(log.IsExpanded);
            Assert.Equal("copied foo.png", Opened(dialog).Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static (ToolInputsDialog Window, ToolInputsViewModel Model) Shown(InstalledTool tool)
    {
        var model = new ToolInputsViewModel(tool, new Dictionary<string, string>(), workspaceRoot: null);
        var window = new ToolInputsDialog { Title = model.Title, DataContext = model };

        window.Show();

        return (window, model);
    }

    /// <summary>
    /// The log's own text, once the expander has drawn its content. An expander realises
    /// nothing until it is open, so nothing inside one is in the visual tree before that.
    /// </summary>
    private static SelectableTextBlock Opened(Visual dialog)
    {
        Pump(dialog);

        return Named<SelectableTextBlock>(dialog, "LogText");
    }

    private static void Pump(Visual dialog)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        (dialog as Avalonia.Layout.Layoutable)?.UpdateLayout();
    }

    private static IEnumerable<T> Visible<T>(Visual window)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Where(control => control.IsVisible);

    private static T Named<T>(Visual window, string name)
        where T : Control =>
        Assert.Single(window.GetVisualDescendants().OfType<T>(), control => control.Name == name);

    private static ToolInput Input(string key, ToolInputKind kind) =>
        new(
            key,
            key,
            "What it is for",
            kind,
            Argument: null,
            Required: false,
            Remember: false,
            Default: string.Empty,
            Choices: [],
            Minimum: null,
            Maximum: null);
}
