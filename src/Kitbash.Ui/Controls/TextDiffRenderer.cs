using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The fill behind each row. Drawn the whole width of the view rather than the width of the
/// text, so a run of changed lines reads as a block however short the lines in it are.
/// </summary>
internal sealed class TextDiffRenderer : IBackgroundRenderer
{
    private readonly TextDiff _owner;

    internal TextDiffRenderer(TextDiff owner)
    {
        _owner = owner;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (!textView.VisualLinesValid)
        {
            return;
        }

        var width = textView.Bounds.Width;

        foreach (var line in textView.VisualLines)
        {
            if (line.IsDisposed || line.FirstDocumentLine is not { IsDeleted: false } document)
            {
                continue;
            }

            if (_owner.At(document.LineNumber) is not { } info)
            {
                continue;
            }

            if (_owner.Style(info.Kind)?.Fill is not { } fill)
            {
                continue;
            }

            var top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop)
                - textView.VerticalOffset;
            var bottom = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom)
                - textView.VerticalOffset;

            drawingContext.FillRectangle(fill, new Rect(0, top, width, bottom - top));
        }
    }
}
