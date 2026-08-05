using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The ink of a line and the words marked inside it. Both are run properties over a range,
/// so the editor lays the text out once and nothing here draws.
/// </summary>
internal sealed class TextDiffColouriser : DocumentColorizingTransformer
{
    private readonly TextDiff _owner;

    internal TextDiffColouriser(TextDiff owner)
    {
        _owner = owner;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (_owner.At(line.LineNumber) is not { } info)
        {
            return;
        }

        var length = line.Length;
        var style = _owner.Style(info.Kind);

        if (style?.Ink is { } ink)
        {
            ChangeLinePart(line.Offset, line.EndOffset, part =>
                part.TextRunProperties.SetForegroundBrush(ink));
        }

        // A grammar runs before the changed words, so a marked word keeps whatever colour the
        // grammar gave it and only gains a background.
        foreach (var colour in _owner.Colouring?.Colour(info.Text, _owner.Path) ?? [])
        {
            if (Clamp(colour.Span, length) is not { } span)
            {
                continue;
            }

            ChangeLinePart(line.Offset + span.Start, line.Offset + span.End, part =>
                part.TextRunProperties.SetForegroundBrush(colour.Brush));
        }

        if (style?.Highlight is not { } highlight)
        {
            return;
        }

        foreach (var run in info.Changed)
        {
            if (Clamp(run, length) is not { } span)
            {
                continue;
            }

            ChangeLinePart(line.Offset + span.Start, line.Offset + span.End, part =>
                part.TextRunProperties.SetBackgroundBrush(highlight));
        }
    }

    /// <summary>
    /// A run inside the line, or null when there is nothing of it left. The spans are worked
    /// out from the caller's own text, so a shorter line here would be an offset past the end.
    /// </summary>
    private static TextDiffSpan? Clamp(TextDiffSpan span, int length)
    {
        var start = Math.Max(span.Start, 0);
        var end = Math.Min(span.End, length);

        return end > start ? new TextDiffSpan(start, end - start) : null;
    }
}
