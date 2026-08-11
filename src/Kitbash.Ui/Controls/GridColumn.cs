using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One column, shared by the flat grid and the tree grid. It says how wide the column is
/// and what a cell in it looks like, and nothing about which grid is drawing it.
/// </summary>
public class GridColumn : AvaloniaObject
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<GridColumn, object?>(nameof(Header));

    /// <summary>
    /// Pixel or star. Auto is refused: it would have to measure every row, and a width
    /// that only works while the whole set is realised is the one thing a virtualised
    /// grid cannot have.
    /// </summary>
    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<GridColumn, GridLength>(
            nameof(Width),
            new GridLength(1, GridUnitType.Star),
            validate: width => !width.IsAuto);

    public static readonly StyledProperty<double> MinWidthProperty =
        AvaloniaProperty.Register<GridColumn, double>(nameof(MinWidth), 40d);

    public static readonly StyledProperty<double> MaxWidthProperty =
        AvaloniaProperty.Register<GridColumn, double>(nameof(MaxWidth), double.PositiveInfinity);

    public static readonly StyledProperty<bool> CanResizeProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(CanResize), true);

    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(IsVisible), true);

    /// <summary>
    /// Whether this column is held against the left edge while the rest scroll under it.
    /// Pinning is to the left only, which is what the design draws.
    /// </summary>
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(IsPinned));

    /// <summary>Whether this column may be pinned at all. The grid has to allow it too.</summary>
    public static readonly StyledProperty<bool> CanPinProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(CanPin), true);

    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<GridColumn, IDataTemplate?>(nameof(CellTemplate));

    /// <summary>What a cell turns into while it is being edited. No template, no edit.</summary>
    public static readonly StyledProperty<IDataTemplate?> EditTemplateProperty =
        AvaloniaProperty.Register<GridColumn, IDataTemplate?>(nameof(EditTemplate));

    public static readonly StyledProperty<HorizontalAlignment> AlignmentProperty =
        AvaloniaProperty.Register<GridColumn, HorizontalAlignment>(nameof(Alignment), HorizontalAlignment.Stretch);

    /// <summary>
    /// Whether this column holds values rather than language. Identifiers, counts, paths
    /// and numbers are mono, names and labels are not.
    /// </summary>
    public static readonly StyledProperty<bool> IsMonoProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(IsMono));

    /// <summary>
    /// Whether this column says which row it is rather than something about it. An
    /// identifier and a name lead, and everything beside them rests a step quieter.
    /// </summary>
    public static readonly StyledProperty<bool> IsStrongProperty =
        AvaloniaProperty.Register<GridColumn, bool>(nameof(IsStrong));

    public static readonly StyledProperty<GridSortDirection> SortDirectionProperty =
        AvaloniaProperty.Register<GridColumn, GridSortDirection>(nameof(SortDirection));

    /// <summary>
    /// Where this column comes in a sort of more than one, counting from one. Zero when the
    /// sort is on this column alone, so a header only draws an ordinal when there is a
    /// second column to tell it from.
    /// </summary>
    public static readonly StyledProperty<int> SortOrderProperty =
        AvaloniaProperty.Register<GridColumn, int>(nameof(SortOrder));

    static GridColumn()
    {
        WidthProperty.Changed.AddClassHandler<GridColumn>((column, _) => column.RaiseLayoutChanged());
        MinWidthProperty.Changed.AddClassHandler<GridColumn>((column, _) => column.RaiseLayoutChanged());
        MaxWidthProperty.Changed.AddClassHandler<GridColumn>((column, _) => column.RaiseLayoutChanged());
        IsVisibleProperty.Changed.AddClassHandler<GridColumn>((column, _) => column.RaiseLayoutChanged());
        IsPinnedProperty.Changed.AddClassHandler<GridColumn>((column, _) => column.RaiseLayoutChanged());
    }

    /// <summary>Raised when something that decides the column's width has moved.</summary>
    internal event EventHandler? LayoutChanged;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public GridLength Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public double MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public double MaxWidth
    {
        get => GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public bool CanResize
    {
        get => GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <inheritdoc cref="IsPinnedProperty"/>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <inheritdoc cref="CanPinProperty"/>
    public bool CanPin
    {
        get => GetValue(CanPinProperty);
        set => SetValue(CanPinProperty, value);
    }

    public IDataTemplate? CellTemplate
    {
        get => GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public IDataTemplate? EditTemplate
    {
        get => GetValue(EditTemplateProperty);
        set => SetValue(EditTemplateProperty, value);
    }

    public HorizontalAlignment Alignment
    {
        get => GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    public bool IsMono
    {
        get => GetValue(IsMonoProperty);
        set => SetValue(IsMonoProperty, value);
    }

    public bool IsStrong
    {
        get => GetValue(IsStrongProperty);
        set => SetValue(IsStrongProperty, value);
    }

    public GridSortDirection SortDirection
    {
        get => GetValue(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    /// <inheritdoc cref="SortOrderProperty"/>
    public int SortOrder
    {
        get => GetValue(SortOrderProperty);
        set => SetValue(SortOrderProperty, value);
    }

    /// <summary>
    /// What to sort a row by. Null means the column cannot be sorted, so its header does
    /// nothing when it is clicked and draws no indicator.
    /// </summary>
    public Func<object, object?>? SortKey { get; set; }

    /// <summary>
    /// How two of this column's keys order, or null for the built in one, which compares
    /// numbers as numbers and text the way a person reads it.
    /// </summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// This column's value for an item, which is what copy writes out. A column that can be
    /// sorted has already said what its value is, so this only has to be set for one that
    /// cannot. Neither one means the column copies as nothing.
    /// </summary>
    public Func<object, object?>? Value { get; set; }

    /// <summary>
    /// Puts a value into this column for an item, as the text that arrived. Only the tool
    /// knows how to read that text, so the tool does the reading. Null means the column
    /// takes nothing, which is what makes paste, cut and clear pass it by.
    /// </summary>
    public Action<object, string?>? Write { get; set; }

    /// <summary>Whether a header click does anything.</summary>
    public bool CanSort => SortKey is not null;

    /// <summary>Whether anything can put a value into this column.</summary>
    public bool CanWrite => Write is not null;

    /// <summary>Whether this column says anything about an item, which copy needs it to.</summary>
    internal bool HasValue => Value is not null || SortKey is not null;

    /// <summary>What this column holds for an item, or null when it says nothing about one.</summary>
    internal object? ValueOf(object item) => (Value ?? SortKey)?.Invoke(item);

    /// <summary>Up, then down, then back to the order the source came in.</summary>
    internal GridSortDirection NextSort() => SortDirection switch
    {
        GridSortDirection.Ascending => GridSortDirection.Descending,
        GridSortDirection.Descending => GridSortDirection.None,
        _ => GridSortDirection.Ascending,
    };

    /// <summary>How wide the column came out. Written by <see cref="GridColumns"/> alone.</summary>
    public double ActualWidth { get; internal set; }

    /// <summary>How far in the column starts. Written by <see cref="GridColumns"/> alone.</summary>
    public double Offset { get; internal set; }

    private void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);
}
