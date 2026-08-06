using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What every gutter beside a diff shares. Each draws one thing per line that can be seen,
/// and asks the editor above it for the line rather than holding one of its own.
/// </summary>
public abstract class TextDiffMargin : AbstractMargin
{
    /// <summary>The line a drag down the gutter started on, or zero when none is running.</summary>
    private int _anchor;

    protected TextDiffMargin()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    protected TextDiff? Owner => this.FindAncestorOfType<TextDiff>();

    /// <summary>
    /// A press on any gutter takes the whole line, and dragging takes every line it passes.
    /// Without this the press reaches the text area instead, which reads the pointer as being
    /// left of the text and leaves the caret at the start of the line rather than the end.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Owner is null)
        {
            return;
        }

        _anchor = LineAt(e.GetPosition(this).Y);

        if (_anchor == 0)
        {
            return;
        }

        Take(_anchor, _anchor);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerMoved(e);

        if (_anchor == 0 || !ReferenceEquals(e.Pointer.Captured, this))
        {
            return;
        }

        if (LineAt(e.GetPosition(this).Y) is var line and not 0)
        {
            Take(_anchor, line);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerReleased(e);

        if (_anchor != 0)
        {
            _anchor = 0;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Selects whole lines, from the start of the first to the end of the last, whichever way
    /// round they were dragged. Ending at the end is the point: it is what puts the caret past
    /// the last line's text rather than before it.
    /// </summary>
    private void Take(int from, int to)
    {
        if (Owner is not { Document: { } document } owner)
        {
            return;
        }

        var first = Math.Clamp(Math.Min(from, to), 1, document.LineCount);
        var last = Math.Clamp(Math.Max(from, to), 1, document.LineCount);

        var start = document.GetLineByNumber(first).Offset;
        var end = document.GetLineByNumber(last).EndOffset;

        owner.Select(start, end - start);
    }

    /// <summary>
    /// The document line at this point down the gutter, or zero when there is no document. A
    /// point above or below the text answers the nearest line, so a drag off either end takes
    /// everything up to it rather than stopping.
    /// </summary>
    private int LineAt(double y)
    {
        var view = TextView;

        if (Owner is not { Document: { } document } || view is not { VisualLinesValid: true })
        {
            return 0;
        }

        if (view.GetVisualLineFromVisualTop(y + view.VerticalOffset) is { } line
            && line.FirstDocumentLine is { IsDeleted: false } first)
        {
            return first.LineNumber;
        }

        return y <= 0 ? 1 : document.LineCount;
    }

    /// <summary>
    /// Walks the lines on screen, handing each one its diff line and the middle of the row it
    /// was drawn on. Skips a line the editor has thrown away mid scroll.
    /// </summary>
    protected void EachVisible(Action<TextDiffLine, VisualLine, double> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        var owner = Owner;
        var view = TextView;

        if (owner is null || view is not { VisualLinesValid: true })
        {
            return;
        }

        foreach (var line in view.VisualLines)
        {
            if (line.IsDisposed || line.FirstDocumentLine is not { IsDeleted: false } document)
            {
                continue;
            }

            if (owner.At(document.LineNumber) is not { } info)
            {
                continue;
            }

            var middle =
                line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle)
                - view.VerticalOffset;

            draw(info, line, middle);
        }
    }

    protected FormattedText Text(string text, IBrush? brush, double size) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Owner is { } owner
                ? new Typeface(owner.FontFamily, owner.FontStyle, owner.FontWeight)
                : Typeface.Default,
            size,
            brush);
}

/// <summary>
/// The line's own number in its file, which is what a person matches against their editor.
/// Right aligned, so the digits line up however many there are.
/// </summary>
public sealed class TextDiffNumberMargin : TextDiffMargin
{
    /// <summary>The gap between the digits and the mark beside them.</summary>
    private const double Gap = 10;

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var owner = Owner;

        if (owner is null)
        {
            return;
        }

        var size = owner.FontSize;
        var ink = owner.NumberInk;

        EachVisible((info, _, middle) =>
        {
            if (info.Number is not { } number)
            {
                return;
            }

            var text = Text(number.ToString(CultureInfo.InvariantCulture), ink, size);

            context.DrawText(text, new Point(Bounds.Width - Gap - text.Width, middle - (text.Height * 0.5)));
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;

        if (owner is null)
        {
            return new Size(0, 0);
        }

        // Measured against the widest number in the file, so the gutter does not resize as
        // the document is scrolled past line 99.
        var widest = Math.Max(owner.WidestNumber, 1);
        var text = Text(widest.ToString(CultureInfo.InvariantCulture), Brushes.White, owner.FontSize);

        return new Size(text.Width + (Gap * 2), 0);
    }
}

/// <summary>
/// The bar down the left of a row. The one part carrying its kind's colour at full strength,
/// so what happened to a line is readable without reading the line.
/// </summary>
public sealed class TextDiffMarkMargin : TextDiffMargin
{
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var owner = Owner;
        var view = TextView;

        if (owner is null || view is null)
        {
            return;
        }

        EachVisible((info, line, _) =>
        {
            if (owner.Style(info.Kind)?.Mark is not { } mark)
            {
                return;
            }

            var top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop)
                - view.VerticalOffset;
            var bottom = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom)
                - view.VerticalOffset;

            context.FillRectangle(mark, new Rect(0, top, Bounds.Width, bottom - top));
        });
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Owner?.MarkWidth ?? 0, 0);
}

/// <summary>The plus or minus beside a line, in the ink of its own kind.</summary>
public sealed class TextDiffSymbolMargin : TextDiffMargin
{
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var owner = Owner;

        if (owner is null)
        {
            return;
        }

        var size = owner.FontSize;

        EachVisible((info, _, middle) =>
        {
            if (info.Symbol.Length == 0)
            {
                return;
            }

            var text = Text(info.Symbol, owner.Style(info.Kind)?.Ink, size);

            context.DrawText(
                text,
                new Point((Bounds.Width - text.Width) * 0.5, middle - (text.Height * 0.5)));
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;

        if (owner is null)
        {
            return new Size(0, 0);
        }

        return new Size(Text("+", Brushes.White, owner.FontSize).Width * 3, 0);
    }
}
