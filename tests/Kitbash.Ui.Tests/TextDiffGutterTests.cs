using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Dragging down the gutter beside a diff. It takes whole lines, ending past the last line's
/// text rather than before it, which is what the run a gesture acts on is read from.
/// </summary>
public class TextDiffGutterTests
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

    private static (Window Window, TextDiff Diff) Open()
    {
        var diff = new TextDiff
        {
            Lines = Sample(),
            Picking = TextDiffPicking.Lines,
            Actions = TextDiffActions.Stage,
        };

        var window = new Window { Width = 720, Height = 240, Content = diff };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        diff.TextArea.TextView.EnsureVisualLines();

        return (window, diff);
    }

    private static TextDiffNumberMargin Numbers(TextDiff diff) =>
        diff.TextArea.LeftMargins.OfType<TextDiffNumberMargin>().Single();

    /// <summary>Where a line's row sits in the window, for pointing at its number.</summary>
    private static Point Row(Window window, TextDiff diff, int line)
    {
        var view = diff.TextArea.TextView;
        var visual = view.GetVisualLine(line)!;

        var top = visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.LineTop);
        var bottom = visual.GetTextLineVisualYPosition(visual.TextLines[^1], VisualYPosition.LineBottom);

        var margin = Numbers(diff);
        var middle = ((top + bottom) * 0.5) - view.VerticalOffset;

        return margin.TranslatePoint(new Point(margin.Bounds.Width * 0.5, middle), window)!.Value;
    }

    private static void Drag(Window window, TextDiff diff, int from, int to)
    {
        window.MouseDown(Row(window, diff, from), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(Row(window, diff, to));
        Dispatcher.UIThread.RunJobs();

        window.MouseUp(Row(window, diff, to), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Where the selection ends, which is the end a caret is left at.</summary>
    private static int End(TextDiff diff) => diff.SelectionStart + diff.SelectionLength;

    [AvaloniaFact]
    public void ClickingANumberTakesThatWholeLine()
    {
        var (window, diff) = Open();

        Drag(window, diff, 4, 4);

        Assert.Equal((4, 4), diff.SelectedLines);
        Assert.Equal(diff.Document.GetLineByNumber(4).Offset, diff.SelectionStart);
        Assert.Equal(diff.Document.GetLineByNumber(4).EndOffset, End(diff));

        window.Close();
    }

    // The bug this fixes: the press used to reach the text area, which reads the pointer as
    // being left of the text, so the selection ended at the start of the last line.
    [AvaloniaFact]
    public void DraggingDownTheNumbersEndsPastTheLastLinesText()
    {
        var (window, diff) = Open();

        Drag(window, diff, 4, 6);

        Assert.Equal((4, 6), diff.SelectedLines);

        var last = diff.Document.GetLineByNumber(6);

        Assert.Equal(last.EndOffset, End(diff));
        Assert.NotEqual(last.Offset, End(diff));

        window.Close();
    }

    [AvaloniaFact]
    public void DraggingUpwardsTakesTheSameLines()
    {
        var (window, diff) = Open();

        Drag(window, diff, 6, 4);

        Assert.Equal((4, 6), diff.SelectedLines);
        Assert.Equal(diff.Document.GetLineByNumber(4).Offset, diff.SelectionStart);
        Assert.Equal(diff.Document.GetLineByNumber(6).EndOffset, End(diff));

        window.Close();
    }

    // The whole point of dragging the gutter: the run a gesture acts on is read off the
    // selection, so it has to be the lines that were dragged over.
    [AvaloniaFact]
    public void TheDraggedLinesAreTheRunAGestureWouldActOn()
    {
        var (window, diff) = Open();

        Drag(window, diff, 4, 6);

        Assert.NotNull(diff.Chunk);
        Assert.Equal((4, 6), (diff.Chunk!.First, diff.Chunk.Last));

        window.Close();
    }

    // Every gutter is the same strip to a person, so the mark and the symbol select too.
    [AvaloniaFact]
    public void TheOtherGuttersSelectTheSameWay()
    {
        var (window, diff) = Open();

        var symbol = diff.TextArea.LeftMargins.OfType<TextDiffSymbolMargin>().Single();
        var view = diff.TextArea.TextView;
        var visual = view.GetVisualLine(5)!;

        var middle =
            visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.LineMiddle)
            - view.VerticalOffset;

        var point = symbol.TranslatePoint(new Point(symbol.Bounds.Width * 0.5, middle), window)!.Value;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((5, 5), diff.SelectedLines);

        window.Close();
    }
}
