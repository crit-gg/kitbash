using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A diff read as text. Built on a read only editor, so selecting and copying across lines is
/// the editor's own and only the diff's gutters, fills and marks are drawn here.
/// </summary>
public class TextDiff : TextEditor
{
    /// <summary>The lines to draw. One line of this list is one line of the document.</summary>
    public static readonly StyledProperty<IReadOnlyList<TextDiffLine>> LinesProperty =
        AvaloniaProperty.Register<TextDiff, IReadOnlyList<TextDiffLine>>(nameof(Lines), []);

    /// <inheritdoc cref="ITextDiffColouring"/>
    public static readonly StyledProperty<ITextDiffColouring?> ColouringProperty =
        AvaloniaProperty.Register<TextDiff, ITextDiffColouring?>(nameof(Colouring));

    /// <summary>The file the lines came from, which is what a colouring picks a grammar by.</summary>
    public static readonly StyledProperty<string> PathProperty =
        AvaloniaProperty.Register<TextDiff, string>(nameof(Path), "");

    /// <summary>The ink of the line numbers, which belong to no kind.</summary>
    public static readonly StyledProperty<IBrush?> NumberInkProperty =
        AvaloniaProperty.Register<TextDiff, IBrush?>(nameof(NumberInk));

    /// <summary>
    /// What a selection is drawn on. Held here and pushed down, since it belongs to the text
    /// area and a style cannot reach one through the editor's scroller.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SelectionFillProperty =
        AvaloniaProperty.Register<TextDiff, IBrush?>(nameof(SelectionFill));

    /// <inheritdoc cref="SelectionFillProperty"/>
    public static readonly StyledProperty<IBrush?> SelectionInkProperty =
        AvaloniaProperty.Register<TextDiff, IBrush?>(nameof(SelectionInk));

    /// <summary>How wide the mark down the left of a row is, in pixels.</summary>
    public static readonly StyledProperty<double> MarkWidthProperty =
        AvaloniaProperty.Register<TextDiff, double>(nameof(MarkWidth), 3);

    /// <summary>The row height as a multiple of the font's own, which is what sets the ramp.</summary>
    public static readonly StyledProperty<double> LineHeightFactorProperty =
        AvaloniaProperty.Register<TextDiff, double>(nameof(LineHeightFactor), 1.3);

    public static readonly StyledProperty<TextDiffKindStyle?> ContextStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(ContextStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> AddedStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(AddedStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> RemovedStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(RemovedStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> OursStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(OursStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> TheirsStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(TheirsStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> ChosenStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(ChosenStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> SettledStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(SettledStyle));

    public static readonly StyledProperty<TextDiffKindStyle?> HeadingStyleProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffKindStyle?>(nameof(HeadingStyle));

    /// <inheritdoc cref="TextDiffPicking"/>
    public static readonly StyledProperty<TextDiffPicking> PickingProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffPicking>(nameof(Picking));

    /// <summary>Which gestures a picked run is offered.</summary>
    public static readonly StyledProperty<TextDiffActions> ActionsProperty =
        AvaloniaProperty.Register<TextDiff, TextDiffActions>(nameof(Actions));

    /// <summary>The run of lines the selection covers, or null when nothing is selected.</summary>
    public static readonly DirectProperty<TextDiff, TextDiffChunk?> ChunkProperty =
        AvaloniaProperty.RegisterDirect<TextDiff, TextDiffChunk?>(nameof(Chunk), o => o.Chunk);

    private TextDiffChunk? _chunk;

    /// <summary>A run of lines was asked to do something.</summary>
    public event EventHandler<TextDiffChunkEventArgs>? Asked;

    public TextDiff()
    {
        IsReadOnly = true;
        ShowLineNumbers = false;

        Options.AllowScrollBelowDocument = false;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        Options.EnableTextDragDrop = false;
        Options.HighlightCurrentLine = false;
        Options.EnableRectangularSelection = false;

        TextArea.LeftMargins.Add(new TextDiffNumberMargin());
        TextArea.LeftMargins.Add(new TextDiffMarkMargin());
        TextArea.LeftMargins.Add(new TextDiffSymbolMargin());

        TextArea.TextView.BackgroundRenderers.Add(new TextDiffRenderer(this));
        TextArea.TextView.LineTransformers.Add(new TextDiffColouriser(this));

        TextArea.SelectionChanged += (_, _) => Track();
        TextArea.TextView.ScrollOffsetChanged += (_, _) => Track();

        // A selection can be set while the lines it covers have not been laid out yet, and
        // scrolling rebuilds them, so the run is worked out again whenever they land.
        TextArea.TextView.VisualLinesChanged += (_, _) => Track();
    }

    /// <inheritdoc cref="LinesProperty"/>
    public IReadOnlyList<TextDiffLine> Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc cref="ColouringProperty"/>
    public ITextDiffColouring? Colouring
    {
        get => GetValue(ColouringProperty);
        set => SetValue(ColouringProperty, value);
    }

    /// <inheritdoc cref="PathProperty"/>
    public string Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <inheritdoc cref="NumberInkProperty"/>
    public IBrush? NumberInk
    {
        get => GetValue(NumberInkProperty);
        set => SetValue(NumberInkProperty, value);
    }

    /// <inheritdoc cref="SelectionFillProperty"/>
    public IBrush? SelectionFill
    {
        get => GetValue(SelectionFillProperty);
        set => SetValue(SelectionFillProperty, value);
    }

    /// <inheritdoc cref="SelectionInkProperty"/>
    public IBrush? SelectionInk
    {
        get => GetValue(SelectionInkProperty);
        set => SetValue(SelectionInkProperty, value);
    }

    /// <inheritdoc cref="MarkWidthProperty"/>
    public double MarkWidth
    {
        get => GetValue(MarkWidthProperty);
        set => SetValue(MarkWidthProperty, value);
    }

    /// <inheritdoc cref="LineHeightFactorProperty"/>
    public double LineHeightFactor
    {
        get => GetValue(LineHeightFactorProperty);
        set => SetValue(LineHeightFactorProperty, value);
    }

    public TextDiffKindStyle? ContextStyle
    {
        get => GetValue(ContextStyleProperty);
        set => SetValue(ContextStyleProperty, value);
    }

    public TextDiffKindStyle? AddedStyle
    {
        get => GetValue(AddedStyleProperty);
        set => SetValue(AddedStyleProperty, value);
    }

    public TextDiffKindStyle? RemovedStyle
    {
        get => GetValue(RemovedStyleProperty);
        set => SetValue(RemovedStyleProperty, value);
    }

    public TextDiffKindStyle? OursStyle
    {
        get => GetValue(OursStyleProperty);
        set => SetValue(OursStyleProperty, value);
    }

    public TextDiffKindStyle? TheirsStyle
    {
        get => GetValue(TheirsStyleProperty);
        set => SetValue(TheirsStyleProperty, value);
    }

    public TextDiffKindStyle? ChosenStyle
    {
        get => GetValue(ChosenStyleProperty);
        set => SetValue(ChosenStyleProperty, value);
    }

    public TextDiffKindStyle? SettledStyle
    {
        get => GetValue(SettledStyleProperty);
        set => SetValue(SettledStyleProperty, value);
    }

    public TextDiffKindStyle? HeadingStyle
    {
        get => GetValue(HeadingStyleProperty);
        set => SetValue(HeadingStyleProperty, value);
    }

    /// <summary>
    /// The lines the selection touches, counting from one and taking both ends, or null when
    /// nothing is selected. This is what staging a run of lines is picked with.
    /// </summary>
    public (int First, int Last)? SelectedLines
    {
        get
        {
            var selection = TextArea.Selection;

            if (selection.IsEmpty)
            {
                return null;
            }

            var start = selection.StartPosition.Line;
            var end = selection.EndPosition.Line;

            return start <= end ? (start, end) : (end, start);
        }
    }

    /// <inheritdoc cref="PickingProperty"/>
    public TextDiffPicking Picking
    {
        get => GetValue(PickingProperty);
        set => SetValue(PickingProperty, value);
    }

    /// <inheritdoc cref="ActionsProperty"/>
    public TextDiffActions Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <inheritdoc cref="ChunkProperty"/>
    public TextDiffChunk? Chunk
    {
        get => _chunk;
        private set => SetAndRaise(ChunkProperty, ref _chunk, value);
    }

    /// <summary>
    /// The widest line number the gutter has to fit, which is what it measures itself by.
    /// </summary>
    internal int WidestNumber { get; private set; }

    /// <summary>Asks the run of lines currently picked to do something.</summary>
    public void Ask(TextDiffActions action)
    {
        if (Chunk is { } chunk && (Actions & action) == action && action != TextDiffActions.None)
        {
            Asked?.Invoke(this, new TextDiffChunkEventArgs(action, chunk));
        }
    }

    /// <summary>
    /// Works out what a gesture would act on, which is the selection and nothing else. A
    /// person has to have said what they meant before a button to change it is drawn.
    /// </summary>
    private void Track()
    {
        var view = TextArea.TextView;
        var selection = TextArea.Selection;

        if (Picking == TextDiffPicking.None
            || !view.VisualLinesValid
            || Lines.Count == 0
            || selection.IsEmpty)
        {
            Chunk = null;
            return;
        }

        var start = selection.StartPosition.Line;
        var end = selection.EndPosition.Line;

        Chunk = Between(start <= end ? start : end, start <= end ? end : start);
    }

    /// <summary>
    /// A run of lines and where it is drawn, or null when it holds nothing that could be
    /// staged. A run of context alone is not a change and is never offered.
    /// </summary>
    private TextDiffChunk? Between(int first, int last)
    {
        var changed = false;

        for (var at = first; at <= last && !changed; at++)
        {
            changed = At(at) is { Kind: not TextDiffLineKind.Context and not TextDiffLineKind.Heading };
        }

        if (!changed)
        {
            return null;
        }

        var view = TextArea.TextView;
        var start = view.GetVisualLine(first);
        var end = view.GetVisualLine(last);

        // A run scrolled off the top or the bottom is clamped to the edge it went past, so
        // the buttons stay against the part of it that can still be seen.
        var top = start is null
            ? 0
            : start.GetTextLineVisualYPosition(start.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;

        var bottom = end is null
            ? view.Bounds.Height
            : end.GetTextLineVisualYPosition(end.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset;

        return new TextDiffChunk(first, last, top, bottom - top);
    }

    /// <summary>The line at a document line number, counting from one, or null past the end.</summary>
    public TextDiffLine? At(int number)
    {
        var lines = Lines;

        return number >= 1 && number <= lines.Count ? lines[number - 1] : null;
    }

    /// <summary>What the kind of a line looks like, or null when the theme named nothing.</summary>
    public TextDiffKindStyle? Style(TextDiffLineKind kind) => kind switch
    {
        TextDiffLineKind.Added => AddedStyle,
        TextDiffLineKind.Removed => RemovedStyle,
        TextDiffLineKind.Ours => OursStyle,
        TextDiffLineKind.Theirs => TheirsStyle,
        TextDiffLineKind.Chosen => ChosenStyle,
        TextDiffLineKind.Settled => SettledStyle,
        TextDiffLineKind.Heading => HeadingStyle,
        _ => ContextStyle,
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LinesProperty)
        {
            Fill();
            return;
        }

        if (change.Property == LineHeightFactorProperty)
        {
            Options.LineHeightFactor = LineHeightFactor;
            return;
        }

        if (change.Property == SelectionFillProperty)
        {
            TextArea.SelectionBrush = SelectionFill;
            return;
        }

        if (change.Property == SelectionInkProperty)
        {
            TextArea.SelectionForeground = SelectionInk;
            return;
        }

        // A brush, a width or a grammar arriving from the theme changes what is drawn but
        // not what is in the document, so a redraw is the whole of it.
        if (change.Property == ColouringProperty
            || change.Property == PathProperty
            || change.Property == NumberInkProperty
            || change.Property == MarkWidthProperty
            || change.Property == ContextStyleProperty
            || change.Property == AddedStyleProperty
            || change.Property == RemovedStyleProperty
            || change.Property == OursStyleProperty
            || change.Property == TheirsStyleProperty
            || change.Property == ChosenStyleProperty
            || change.Property == SettledStyleProperty
            || change.Property == HeadingStyleProperty)
        {
            Redraw();
        }
    }

    /// <summary>Puts the lines into the document and lets every margin measure again.</summary>
    private void Fill()
    {
        var lines = Lines;
        var widest = 0;

        var text = string.Join('\n', lines.Select(line =>
        {
            if (line.Number is { } number && number > widest)
            {
                widest = number;
            }

            return Flat(line.Text);
        }));

        WidestNumber = widest;

        Document = new TextDocument(text);

        Redraw();
    }

    private void Redraw()
    {
        foreach (var margin in TextArea.LeftMargins)
        {
            margin.InvalidateMeasure();
            margin.InvalidateVisual();
        }

        TextArea.TextView.Redraw();
    }

    /// <summary>
    /// A diff line never holds a line break, and one that did would put every line after it
    /// against the wrong number, so any break is flattened rather than trusted.
    /// </summary>
    private static string Flat(string text) =>
        text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal)
            ? text.Replace('\r', ' ').Replace('\n', ' ')
            : text;
}
