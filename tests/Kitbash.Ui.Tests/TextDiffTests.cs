using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>The diff control, drawn for real with no display behind it.</summary>
public class TextDiffTests
{
    private static string Shots =>
        Environment.GetEnvironmentVariable("KITBASH_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "kitbash-shots");

    /// <summary>A window holding one diff, laid out and ready to read.</summary>
    private static (Window Window, TextDiff Diff) Open(IReadOnlyList<TextDiffLine> lines)
    {
        var diff = new TextDiff { ItemsSource = lines };

        var window = new Window
        {
            Width = 720,
            Height = 320,
            Content = diff,
        };

        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return (window, diff);
    }

    private static IReadOnlyList<TextDiffRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextDiffRow>()];

    private static TextBlock Part(TextDiffRow row, string name) =>
        row.GetVisualDescendants().OfType<TextBlock>().First(b => b.Name == name);

    private static IReadOnlyList<TextDiffLine> Sample() =>
    [
        new TextDiffLine(TextDiffLineKind.Heading, "@@ -17,7 +17,7 @@ func _ready()"),
        new TextDiffLine(TextDiffLineKind.Context, "\tstate_machine.start()", 17, 17),
        new TextDiffLine(TextDiffLineKind.Removed, "\tposition = Vector2(0, -6)", 18, null),
        new TextDiffLine(TextDiffLineKind.Added, "\tposition = Vector2(0, -8)", null, 18),
        new TextDiffLine(TextDiffLineKind.Context, "\thurt_box.flash()", 19, 19),
    ];

    [AvaloniaFact]
    public void EveryLineIsDrawnWithItsNumberAndItsSymbol()
    {
        var (window, _) = Open(Sample());

        var rows = Rows(window);

        Assert.Equal(5, rows.Count);

        // A heading is not a line of the file, so it carries no number.
        Assert.Equal("", Part(rows[0], "PART_Number").Text);
        Assert.Equal("", Part(rows[0], "PART_Symbol").Text);

        Assert.Equal("17", Part(rows[1], "PART_Number").Text);
        Assert.Equal("", Part(rows[1], "PART_Symbol").Text);

        // A removed line still says where it was, which is its number on the old side.
        Assert.Equal("18", Part(rows[2], "PART_Number").Text);
        Assert.Equal("-", Part(rows[2], "PART_Symbol").Text);

        Assert.Equal("18", Part(rows[3], "PART_Number").Text);
        Assert.Equal("+", Part(rows[3], "PART_Symbol").Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AKindPutsItsOwnClassOnTheRow()
    {
        var (window, _) = Open(Sample());

        var rows = Rows(window);

        Assert.Contains("heading", rows[0].Classes);
        Assert.Contains("context", rows[1].Classes);
        Assert.Contains("removed", rows[2].Classes);
        Assert.Contains("added", rows[3].Classes);

        // Only ever one, or two fills would fight over the same row.
        Assert.All(rows, r => Assert.Single(
            r.Classes,
            c => c is "context" or "added" or "removed" or "ours" or "theirs"
                or "chosen" or "settled" or "heading"));

        window.Close();
    }

    // The mark down the left is the one part of a row that carries colour at full strength,
    // so it is what says what happened without reading the line.
    [AvaloniaFact]
    public void TheMarkTakesTheKindsOwnColour()
    {
        var (window, _) = Open(Sample());

        var rows = Rows(window);

        Border Mark(TextDiffRow row) =>
            row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Mark");

        Assert.Equal(
            Application.Current!.FindResource("Error"),
            Mark(rows[2]).Background);

        Assert.Equal(
            Application.Current!.FindResource("Ok"),
            Mark(rows[3]).Background);

        window.Close();
    }

    // A line with nothing marked is one run of text rather than a list of them, since
    // building runs for every row of a large diff is waste.
    [AvaloniaFact]
    public void AnUnmarkedLineIsPlainText()
    {
        var (window, _) = Open(Sample());

        var text = Part(Rows(window)[1], "PART_Text");

        Assert.Equal("\tstate_machine.start()", text.Text);
        Assert.True(text.Inlines is null || text.Inlines.Count == 0);

        window.Close();
    }

    [AvaloniaFact]
    public void MarkingPairsARemovedLineWithTheAddedOneAfterIt()
    {
        var marked = new TextDiffWords().Mark(Sample());

        var gone = marked[2];
        var came = marked[3];

        Assert.Equal("6", gone.Text.Substring(Assert.Single(gone.Changed).Start, 1));
        Assert.Equal("8", came.Text.Substring(Assert.Single(came.Changed).Start, 1));

        // Nothing else in the run is touched.
        Assert.Empty(marked[1].Changed);
        Assert.Empty(marked[4].Changed);
    }

    [AvaloniaFact]
    public void AMarkedWordIsDrawnOnItsOwnBackground()
    {
        var (window, _) = Open(new TextDiffWords().Mark(Sample()));

        var text = Part(Rows(window)[3], "PART_Text");

        Assert.NotNull(text.Inlines);

        var runs = text.Inlines.OfType<Run>().ToList();
        var lit = runs.Where(r => r.Background is not null).ToList();

        Assert.Equal("8", string.Concat(lit.Select(r => r.Text)));

        // And the rest of the line is still there, unmarked.
        Assert.Equal("\tposition = Vector2(0, -8)", string.Concat(runs.Select(r => r.Text)));

        window.Close();
    }

    // The seam for a grammar. The library ships none, so this proves a supplied one reaches
    // the line without the control knowing anything about languages.
    private sealed class TwoWords : ITextDiffColouring
    {
        public IReadOnlyList<TextDiffColour> Colour(string text, string path)
        {
            var at = text.IndexOf("position", StringComparison.Ordinal);

            return at < 0 || path != "player.gd"
                ? []
                : [new TextDiffColour(new TextDiffSpan(at, "position".Length), Brushes.Magenta)];
        }
    }

    [AvaloniaFact]
    public void ASuppliedColouringReachesTheLine()
    {
        var diff = new TextDiff
        {
            ItemsSource = Sample(),
            Colouring = new TwoWords(),
            Path = "player.gd",
        };

        var window = new Window { Width = 720, Height = 320, Content = diff };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var text = Part(Rows(window)[2], "PART_Text");

        Assert.NotNull(text.Inlines);

        // Foreground on an inline inherits, so the run is found by its text and its brush
        // is read off it rather than asking which runs carry one.
        var runs = text.Inlines.OfType<Run>().ToList();
        var coloured = Assert.Single(runs, r => r.Text == "position");

        Assert.Equal(Brushes.Magenta, coloured.Foreground);
        Assert.Equal("\tposition = Vector2(0, -6)", string.Concat(runs.Select(r => r.Text)));

        window.Close();
    }

    // Staging a hunk is picking a run of lines, so more than one at a time is the point.
    [AvaloniaFact]
    public void MoreThanOneLineCanBePicked()
    {
        var lines = Sample();
        var (window, diff) = Open(lines);

        Assert.Equal(SelectionMode.Multiple, diff.SelectionMode);

        diff.SelectedItems!.Add(lines[2]);
        diff.SelectedItems.Add(lines[3]);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, diff.SelectedItems.Count);

        window.Close();
    }

    // A container is reused for another line, so anything a row is told has to be untold in
    // the same breath or a recycled row wears the line before it.
    [AvaloniaFact]
    public void ARecycledRowWearsNothingOfTheLineBeforeIt()
    {
        var many = new List<TextDiffLine>();

        for (var i = 0; i < 400; i++)
        {
            many.Add(new TextDiffLine(
                i % 3 == 0 ? TextDiffLineKind.Added : TextDiffLineKind.Context,
                "line " + i,
                i + 1,
                i + 1));
        }

        var (window, diff) = Open(many);

        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();

        scroller.Offset = new Avalonia.Vector(0, 2000);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        // Far fewer controls than lines, which is the whole reason this is a list.
        var rows = Rows(window);

        Assert.True(rows.Count < 60, $"{rows.Count} rows were realised for 400 lines");

        foreach (var row in rows)
        {
            var kind = row.Classes.Count(c => c is "added" or "context");

            Assert.Equal(1, kind);
        }

        Directory.CreateDirectory(Shots);

        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(Shots, "text-diff-scrolled.png"), new PngBitmapEncoderOptions());

        window.Close();
    }

    [AvaloniaFact]
    public void TheControlIsDrawn()
    {
        var lines = new TextDiffWords().Mark(
        [
            new TextDiffLine(TextDiffLineKind.Heading, "@@ -14,9 +14,11 @@ func _ready()"),
            new TextDiffLine(TextDiffLineKind.Context, "extends CharacterBody2D", 14, 14),
            new TextDiffLine(TextDiffLineKind.Context, "", 15, 15),
            new TextDiffLine(TextDiffLineKind.Removed, "@onready var state := $StateMachine", 16, null),
            new TextDiffLine(TextDiffLineKind.Added, "@onready var state := $Logic/StateMachine", null, 16),
            new TextDiffLine(TextDiffLineKind.Added, "@onready var hurt_box: Area2D = $HurtBox", null, 17),
            new TextDiffLine(TextDiffLineKind.Context, "", 17, 18),
            new TextDiffLine(TextDiffLineKind.Context, "func _ready() -> void:", 18, 19),
            new TextDiffLine(TextDiffLineKind.Removed, "\tstate.start(\"Idle\")", 19, null),
            new TextDiffLine(TextDiffLineKind.Added, "\tstate.start(\"Walk\")", null, 20),
            new TextDiffLine(TextDiffLineKind.Settled, "\thurt_box.body_entered.connect(_on_hurt)", null, 21),
            new TextDiffLine(TextDiffLineKind.Ours, "\tvelocity.y = jump_force", null, 22),
            new TextDiffLine(TextDiffLineKind.Theirs, "\tvelocity.y = JUMP", null, 23),
            new TextDiffLine(TextDiffLineKind.Chosen, "\tvelocity.y = jump_force", null, 24),
        ]);

        var (window, _) = Open(lines);

        Directory.CreateDirectory(Shots);

        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(Shots, "text-diff.png"), new PngBitmapEncoderOptions());

        window.Close();
    }
}
