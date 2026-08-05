using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
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
        var diff = new TextDiff { Lines = lines };

        var window = new Window
        {
            Width = 720,
            Height = 320,
            Content = diff,
        };

        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        diff.TextArea.TextView.EnsureVisualLines();

        return (window, diff);
    }

    private static IReadOnlyList<TextDiffLine> Sample() =>
    [
        new TextDiffLine(TextDiffLineKind.Heading, "@@ -17,7 +17,7 @@ func _ready()"),
        new TextDiffLine(TextDiffLineKind.Context, "\tstate_machine.start()", 17, 17),
        new TextDiffLine(TextDiffLineKind.Removed, "\tposition = Vector2(0, -6)", 18, null),
        new TextDiffLine(TextDiffLineKind.Added, "\tposition = Vector2(0, -8)", null, 18),
        new TextDiffLine(TextDiffLineKind.Context, "\thurt_box.flash()", 19, 19),
    ];

    /// <summary>The run properties the editor built for one document line.</summary>
    private static IReadOnlyList<VisualLineElement> Parts(TextDiff diff, int number)
    {
        var view = diff.TextArea.TextView;
        var line = view.GetOrConstructVisualLine(diff.Document.GetLineByNumber(number));

        return line.Elements;
    }

    /// <summary>
    /// The colour of a brush. The editor keeps an immutable copy of whatever it is handed, so
    /// a brush from the theme and the one on a run are equal in colour and never the same
    /// object.
    /// </summary>
    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static Color? Token(string key) =>
        Colour(Application.Current!.FindResource(key) as IBrush);

    // The document is the file's own text. The symbol and the number are drawn in the gutters
    // beside it, so nothing a person selects and copies carries them.
    [AvaloniaFact]
    public void TheDocumentIsTheLinesWithNoMarkersInIt()
    {
        var lines = Sample();
        var (window, diff) = Open(lines);

        Assert.Equal(lines.Count, diff.Document.LineCount);

        for (var at = 0; at < lines.Count; at++)
        {
            Assert.Equal(lines[at].Text, diff.Document.GetText(diff.Document.GetLineByNumber(at + 1)));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheDiffCannotBeEdited()
    {
        var (window, diff) = Open(Sample());

        Assert.True(diff.IsReadOnly);

        // Its own numbers are off, since the gutter draws the file's number rather than the
        // line's place in the document.
        Assert.False(diff.ShowLineNumbers);

        window.Close();
    }

    [AvaloniaFact]
    public void AKindCarriesItsFillItsMarkAndItsInkTogether()
    {
        var (window, diff) = Open(Sample());

        var added = diff.Style(TextDiffLineKind.Added);
        var removed = diff.Style(TextDiffLineKind.Removed);

        Assert.Equal(Token("Ok"), Colour(added?.Mark));
        Assert.Equal(Token("OkSurface"), Colour(added?.Fill));
        Assert.Equal(Token("Error"), Colour(removed?.Mark));
        Assert.Equal(Token("ErrorSurface"), Colour(removed?.Fill));

        // A heading is not a side of the diff, so it takes no mark.
        Assert.Null(diff.Style(TextDiffLineKind.Heading)?.Mark);

        window.Close();
    }

    [AvaloniaFact]
    public void ALineIsInkedByItsKind()
    {
        var (window, diff) = Open(Sample());

        Assert.All(
            Parts(diff, 4),
            part => Assert.Equal(Token("OkInk"), Colour(part.TextRunProperties.ForegroundBrush)));

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
        var (window, diff) = Open(new TextDiffWords().Mark(Sample()));

        var lit = Parts(diff, 4).Where(p => p.TextRunProperties.BackgroundBrush is not null).ToList();

        Assert.All(
            lit,
            part => Assert.Equal(Token("OkLine"), Colour(part.TextRunProperties.BackgroundBrush)));

        // Only the one character that changed, and the rest of the line left alone.
        var text = diff.Document.GetText(diff.Document.GetLineByNumber(4));
        var marked = lit.Sum(p => p.DocumentLength);

        Assert.Equal(1, marked);
        Assert.Equal("\tposition = Vector2(0, -8)", text);

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
            Lines = Sample(),
            Colouring = new TwoWords(),
            Path = "player.gd",
        };

        var window = new Window { Width = 720, Height = 320, Content = diff };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        diff.TextArea.TextView.EnsureVisualLines();

        var coloured = Parts(diff, 3)
            .Where(p => Colour(p.TextRunProperties.ForegroundBrush) == Colors.Magenta)
            .ToList();

        Assert.Equal("position".Length, coloured.Sum(p => p.DocumentLength));

        // The grammar colours one word and the kind still inks the rest.
        Assert.Contains(
            Parts(diff, 3),
            p => Colour(p.TextRunProperties.ForegroundBrush) == Token("ErrorInk"));

        window.Close();
    }

    // The reason for the editor. A selection is characters rather than rows, so what is copied
    // is what was dragged over, across as many lines as it touches.
    [AvaloniaFact]
    public void TextCanBeSelectedAndCopiedAcrossLines()
    {
        var lines = Sample();
        var (window, diff) = Open(lines);

        var start = diff.Document.GetLineByNumber(2).Offset;
        var end = diff.Document.GetLineByNumber(4).EndOffset;

        diff.Select(start, end - start);

        Assert.Equal(
            "\tstate_machine.start()\n\tposition = Vector2(0, -6)\n\tposition = Vector2(0, -8)",
            diff.SelectedText.ReplaceLineEndings("\n"));

        // And the same selection read as a run of lines, which is what staging a hunk picks.
        Assert.Equal((2, 4), diff.SelectedLines);

        window.Close();
    }

    [AvaloniaFact]
    public void NothingSelectedIsNoRunOfLines()
    {
        var (window, diff) = Open(Sample());

        Assert.Null(diff.SelectedLines);

        window.Close();
    }

    // A file with thousands of changed lines costs the lines on screen and nothing more.
    [AvaloniaFact]
    public void OnlyTheLinesOnScreenAreBuilt()
    {
        var many = new List<TextDiffLine>();

        for (var i = 0; i < 4000; i++)
        {
            many.Add(new TextDiffLine(
                i % 3 == 0 ? TextDiffLineKind.Added : TextDiffLineKind.Context,
                "line " + i,
                i + 1,
                i + 1));
        }

        var (window, diff) = Open(many);

        Assert.Equal(4000, diff.Document.LineCount);
        Assert.True(
            diff.TextArea.TextView.VisualLines.Count < 60,
            $"{diff.TextArea.TextView.VisualLines.Count} lines were built for 4000");

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
