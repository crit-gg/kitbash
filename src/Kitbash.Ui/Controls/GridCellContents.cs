using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A reference to another record, drawn as a type mark and the name. What a view puts in a
/// <see cref="GridCellKind.Reference"/> column's cell template.
/// </summary>
public class GridRefCell : TemplatedControl
{
    /// <summary>The record's name, which is mono because it is an identifier.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GridRefCell, string?>(nameof(Text));

    /// <summary>
    /// What kind of record it is, as a colour. Null draws no mark at all, for a column whose
    /// rows all point at the same kind of thing.
    /// </summary>
    public static readonly StyledProperty<IBrush?> MarkProperty =
        AvaloniaProperty.Register<GridRefCell, IBrush?>(nameof(Mark));

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="MarkProperty"/>
    public IBrush? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }
}

/// <summary>
/// A fraction, drawn as a bar behind its own value rather than as a widget sitting on it.
/// What a view puts in a <see cref="GridCellKind.Ratio"/> column's cell template.
/// </summary>
public class GridRatioCell : TemplatedControl
{
    /// <summary>Where the bar reaches, from 0 to 1. Anything outside that is clamped.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<GridRatioCell, double>(nameof(Value));

    /// <summary>What the cell says, which is the caller's, since a ratio has many readings.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GridRatioCell, string?>(nameof(Text));

    /// <summary>The bar's own colour. It is a wash, so this is drawn at low opacity.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<GridRatioCell, IBrush?>(nameof(Fill));

    /// <summary>What the bar's width comes to once the value is clamped, as a fraction.</summary>
    public static readonly DirectProperty<GridRatioCell, double> ReachProperty =
        AvaloniaProperty.RegisterDirect<GridRatioCell, double>(nameof(Reach), cell => cell.reach);

    private double reach;

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="ReachProperty"/>
    public double Reach => reach;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            // NaN comes back as nothing rather than as a bar of unknown width, since a
            // ratio nobody has worked out yet is not a ratio of zero.
            var value = change.GetNewValue<double>();

            SetAndRaise(ReachProperty, ref reach, double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1));
        }
    }
}

/// <summary>
/// A list of chips that counts what it could not fit rather than wrapping, since a row is one
/// line high. What a view puts in a <see cref="GridCellKind.Tags"/> column's cell template.
/// </summary>
public class GridTagsCell : TemplatedControl
{
    /// <summary>Every tag, in the order they are drawn.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> TagsProperty =
        AvaloniaProperty.Register<GridTagsCell, IEnumerable<string>?>(nameof(Tags));

    /// <summary>How many chips are drawn before the rest become a count. Two by default.</summary>
    public static readonly StyledProperty<int> ShownProperty =
        AvaloniaProperty.Register<GridTagsCell, int>(nameof(Shown), 2);

    /// <summary>The chips that fit. The first of them leads in the accent.</summary>
    public static readonly DirectProperty<GridTagsCell, IReadOnlyList<GridTag>> DrawnProperty =
        AvaloniaProperty.RegisterDirect<GridTagsCell, IReadOnlyList<GridTag>>(nameof(Drawn), cell => cell.drawn);

    /// <summary>What the rest come to, as words, or empty when they all fit.</summary>
    public static readonly DirectProperty<GridTagsCell, string> OverflowProperty =
        AvaloniaProperty.RegisterDirect<GridTagsCell, string>(nameof(Overflow), cell => cell.overflow);

    private IReadOnlyList<GridTag> drawn = [];
    private string overflow = string.Empty;

    /// <inheritdoc cref="TagsProperty"/>
    public IEnumerable<string>? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    /// <inheritdoc cref="ShownProperty"/>
    public int Shown
    {
        get => GetValue(ShownProperty);
        set => SetValue(ShownProperty, value);
    }

    /// <inheritdoc cref="DrawnProperty"/>
    public IReadOnlyList<GridTag> Drawn => drawn;

    /// <inheritdoc cref="OverflowProperty"/>
    public string Overflow => overflow;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TagsProperty || change.Property == ShownProperty)
        {
            Split();
        }
    }

    /// <summary>
    /// Which tags become chips and what the rest come to. Counted rather than measured, so
    /// the answer does not change as a column is dragged narrower.
    /// </summary>
    private void Split()
    {
        var all = Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList() ?? [];
        var room = Math.Max(0, Shown);
        var rest = all.Count - room;

        SetAndRaise(
            DrawnProperty,
            ref drawn,
            all.Take(room).Select((tag, at) => new GridTag(tag, at == 0)).ToList());
        SetAndRaise(OverflowProperty, ref overflow, rest > 0 ? $"+{rest:N0}" : string.Empty);
    }
}

/// <summary>One chip in a <see cref="GridTagsCell"/>.</summary>
/// <param name="Text">The tag.</param>
/// <param name="IsLead">Whether it is the first, which is the one drawn in the accent.</param>
public sealed record GridTag(string Text, bool IsLead);
