using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The run of lines a gesture would act on, and the buttons over it. What is picked here is
/// what gets staged, so the run has to be exactly the lines a person meant.
/// </summary>
public class TextDiffChunkTests
{
    private static IReadOnlyList<TextDiffLine> Sample() =>
    [
        new TextDiffLine(TextDiffLineKind.Heading, "@@ -14,6 +14,7 @@ func _ready()"),
        new TextDiffLine(TextDiffLineKind.Context, "extends CharacterBody2D", 14, 14),
        new TextDiffLine(TextDiffLineKind.Context, "", 15, 15),
        new TextDiffLine(TextDiffLineKind.Removed, "\tstate.start(\"Idle\")", 16, null),
        new TextDiffLine(TextDiffLineKind.Added, "\tstate.start(\"Walk\")", null, 16),
        new TextDiffLine(TextDiffLineKind.Added, "\thurt_box.flash()", null, 17),
        new TextDiffLine(TextDiffLineKind.Context, "", 17, 18),
        new TextDiffLine(TextDiffLineKind.Context, "func _physics_process(delta):", 18, 19),
    ];

    private static (Window Window, TextDiff Diff, TextDiffBar Bar) Open(
        TextDiffPicking picking, TextDiffActions actions)
    {
        var diff = new TextDiff
        {
            Lines = Sample(),
            Picking = picking,
            Actions = actions,
        };

        var bar = new TextDiffBar { Diff = diff };
        var panel = new Panel();

        panel.Children.Add(diff);
        panel.Children.Add(bar);

        var window = new Window { Width = 720, Height = 240, Content = panel };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        diff.TextArea.TextView.EnsureVisualLines();

        return (window, diff, bar);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.Control, physical, null);

        Dispatcher.UIThread.RunJobs();
    }

    private static void Pick(TextDiff diff, int first, int last)
    {
        var start = diff.Document.GetLineByNumber(first).Offset;
        var end = diff.Document.GetLineByNumber(last).EndOffset;

        diff.Select(start, end - start);

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ASelectionIsTheRunThatWouldBeStaged()
    {
        var (window, diff, _) = Open(TextDiffPicking.Lines, TextDiffActions.Stage);

        Pick(diff, 4, 6);

        Assert.NotNull(diff.Chunk);
        Assert.Equal((4, 6), (diff.Chunk!.First, diff.Chunk.Last));

        window.Close();
    }

    // The rule that guards Godot's own formats. A diff that was never told it could be picked
    // apart offers nothing, however it is selected.
    [AvaloniaFact]
    public void ADiffThatCannotBePickedApartNeverFormsARun()
    {
        var (window, diff, bar) = Open(TextDiffPicking.None, TextDiffActions.Stage);

        Pick(diff, 4, 6);

        Assert.Null(diff.Chunk);
        Assert.False(bar.IsVisible);

        window.Close();
    }

    // Context alone is not a change, so selecting it offers nothing to stage.
    [AvaloniaFact]
    public void ARunOfContextIsNotOffered()
    {
        var (window, diff, _) = Open(TextDiffPicking.Lines, TextDiffActions.Stage);

        Pick(diff, 7, 8);

        Assert.Null(diff.Chunk);

        window.Close();
    }

    [AvaloniaFact]
    public void OnlyTheGesturesOnOfferAreDrawnAndAskedFor()
    {
        var (window, diff, bar) = Open(
            TextDiffPicking.Lines, TextDiffActions.Stage | TextDiffActions.Discard);

        Pick(diff, 4, 6);

        Assert.True(bar.IsVisible);

        List<TextDiffActions> asked = [];
        diff.Asked += (_, e) => asked.Add(e.Action);

        diff.Ask(TextDiffActions.Stage);
        diff.Ask(TextDiffActions.Discard);

        // Never offered, so asking for it does nothing rather than running it.
        diff.Ask(TextDiffActions.Unstage);

        Assert.Equal([TextDiffActions.Stage, TextDiffActions.Discard], asked);

        window.Close();
    }

    // The shortcut and the button are the same gesture, and the shortcut is only alive while a
    // run is picked, so it cannot fire at a diff a person is not looking at.
    [AvaloniaFact]
    public void AShortcutRunsTheGestureItIsOffered()
    {
        var (window, diff, _) = Open(
            TextDiffPicking.Lines, TextDiffActions.Stage | TextDiffActions.Discard);

        List<TextDiffActions> asked = [];
        diff.Asked += (_, e) => asked.Add(e.Action);

        Pick(diff, 4, 6);
        window.UpdateLayout();

        Press(window, Key.S, PhysicalKey.S);

        // Never offered on this diff, so it is not bound and nothing happens.
        Press(window, Key.U, PhysicalKey.U);

        Assert.Equal([TextDiffActions.Stage], asked);

        window.Close();
    }

    // Nothing picked means nothing bound, or a stale shortcut would act on the last run.
    [AvaloniaFact]
    public void AShortcutGoesWithTheRunItActedOn()
    {
        var (window, diff, bar) = Open(TextDiffPicking.Lines, TextDiffActions.Stage);

        Pick(diff, 4, 6);
        window.UpdateLayout();

        Pick(diff, 7, 8);
        window.UpdateLayout();

        Assert.Null(diff.Chunk);
        Assert.False(bar.IsVisible);

        List<TextDiffActions> asked = [];
        diff.Asked += (_, e) => asked.Add(e.Action);

        Press(window, Key.S, PhysicalKey.S);

        Assert.Empty(asked);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBarSitsOverThePickedRun()
    {
        var (window, diff, bar) = Open(
            TextDiffPicking.Lines, TextDiffActions.Stage | TextDiffActions.Discard);

        Pick(diff, 4, 6);
        window.UpdateLayout();

        // Against the top of the run rather than anywhere on the panel.
        Assert.Equal(diff.Chunk!.Top + bar.Drop, bar.Margin.Top, 1);

        var shots = Environment.GetEnvironmentVariable("KITBASH_SHOTS")
            ?? Path.Combine(Path.GetTempPath(), "kitbash-shots");

        Directory.CreateDirectory(shots);

        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(shots, "text-diff-bar.png"), new PngBitmapEncoderOptions());

        window.Close();
    }
}
