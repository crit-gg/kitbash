using Avalonia;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What one kind of diff line looks like. The four parts are held together, so a kind cannot
/// be given a fill without the ink that has to stay readable on it.
/// </summary>
public sealed class TextDiffKindStyle : AvaloniaObject
{
    /// <summary>The fill behind the row, drawn the whole width of the view.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TextDiffKindStyle, IBrush?>(nameof(Fill));

    /// <summary>
    /// The bar down the left. The one part of a row that carries colour at full strength, so
    /// it says what happened without the line being read.
    /// </summary>
    public static readonly StyledProperty<IBrush?> MarkProperty =
        AvaloniaProperty.Register<TextDiffKindStyle, IBrush?>(nameof(Mark));

    /// <summary>The text of the line, and the symbol in the gutter beside it.</summary>
    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<TextDiffKindStyle, IBrush?>(nameof(Ink));

    /// <summary>What a changed word sits on.</summary>
    public static readonly StyledProperty<IBrush?> HighlightProperty =
        AvaloniaProperty.Register<TextDiffKindStyle, IBrush?>(nameof(Highlight));

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="MarkProperty"/>
    public IBrush? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc cref="InkProperty"/>
    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    /// <inheritdoc cref="HighlightProperty"/>
    public IBrush? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }
}
