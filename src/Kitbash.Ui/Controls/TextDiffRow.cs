using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One line of a diff. A list item, so selection, recycling and the row states are the ones
/// every other list here uses.
/// </summary>
[TemplatePart(NumberPart, typeof(TextBlock))]
[TemplatePart(SymbolPart, typeof(TextBlock))]
[TemplatePart(TextPart, typeof(TextBlock))]
public class TextDiffRow : ListBoxItem
{
    private const string NumberPart = "PART_Number";
    private const string SymbolPart = "PART_Symbol";
    private const string TextPart = "PART_Text";

    /// <summary>Every kind's class, so following a row can clear the one before it.</summary>
    private static readonly string[] Kinds =
        ["context", "added", "removed", "ours", "theirs", "chosen", "settled", "heading"];

    private TextBlock? _number;
    private TextBlock? _symbol;
    private TextBlock? _text;
    private TextDiffLine? _line;

    protected override Type StyleKeyOverride => typeof(TextDiffRow);

    /// <summary>
    /// Reads a line onto this row, or clears it. Everything a row is told is told here and
    /// untold here, since a container is reused for another line.
    /// </summary>
    internal void Follow(TextDiffLine? line, ITextDiffColouring? colouring, string path)
    {
        _line = line;

        for (var index = 0; index < Kinds.Length; index++)
        {
            Classes.Set(Kinds[index], line is not null && (int)line.Kind == index);
        }

        Show(colouring, path);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _number = e.NameScope.Find<TextBlock>(NumberPart);
        _symbol = e.NameScope.Find<TextBlock>(SymbolPart);
        _text = e.NameScope.Find<TextBlock>(TextPart);

        Show((this.FindAncestorOfType<TextDiff>())?.Colouring, (this.FindAncestorOfType<TextDiff>())?.Path ?? "");
    }

    private void Show(ITextDiffColouring? colouring, string path)
    {
        if (_number is null || _symbol is null || _text is null)
        {
            return;
        }

        if (_line is not { } line)
        {
            _number.Text = "";
            _symbol.Text = "";
            _text.Text = "";
            _text.Inlines?.Clear();
            return;
        }

        _number.Text = line.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        _symbol.Text = line.Symbol;

        var colours = colouring?.Colour(line.Text, path) ?? [];

        // The common line has nothing to mark, and building runs for one is waste on every
        // row of a large diff.
        if (colours.Count == 0 && line.Changed.Count == 0)
        {
            _text.Inlines?.Clear();
            _text.Text = line.Text;
            return;
        }

        _text.Text = null;
        Fill(_text, line, colours);
    }

    /// <summary>
    /// Builds the line as runs. Worked out a character at a time and then joined, since a
    /// coloured run and a changed run do not line up and either may split the other.
    /// </summary>
    private void Fill(TextBlock text, TextDiffLine line, IReadOnlyList<TextDiffColour> colours)
    {
        var length = line.Text.Length;
        var foreground = new IBrush?[length];
        var marked = new bool[length];

        foreach (var colour in colours)
        {
            for (var at = Math.Max(colour.Span.Start, 0); at < Math.Min(colour.Span.End, length); at++)
            {
                foreground[at] = colour.Brush;
            }
        }

        foreach (var run in line.Changed)
        {
            for (var at = Math.Max(run.Start, 0); at < Math.Min(run.End, length); at++)
            {
                marked[at] = true;
            }
        }

        var highlight = Highlight;

        text.Inlines ??= [];
        text.Inlines.Clear();

        var start = 0;

        for (var at = 1; at <= length; at++)
        {
            if (at < length
                && ReferenceEquals(foreground[at], foreground[start])
                && marked[at] == marked[start])
            {
                continue;
            }

            var piece = new Run(line.Text[start..at]);

            if (foreground[start] is { } brush)
            {
                piece.Foreground = brush;
            }

            if (marked[start] && highlight is not null)
            {
                piece.Background = highlight;
            }

            text.Inlines.Add(piece);
            start = at;
        }
    }

    /// <summary>What a changed word sits on, which the theme sets per kind.</summary>
    private IBrush? Highlight => GetValue(TextDiff.HighlightProperty);
}
