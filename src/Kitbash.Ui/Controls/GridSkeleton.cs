using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What a grid draws while its rows are being fetched. Rows at the real row height with a
/// bar where each value will be, so nothing moves when the data lands. Flat and still, since
/// a grid can reload on every keystroke of a filter.
/// </summary>
public class GridSkeleton : Control
{
    /// <summary>
    /// How wide a bar is as a fraction of its cell, cycled across the rows and the columns
    /// so the block reads as text rather than as a table of blocks.
    /// </summary>
    private static readonly double[] Ratios = [0.78, 0.54, 0.88, 0.62, 0.70, 0.46];

    /// <summary>Enough to fill any grid on any screen, so a bad height cannot spin.</summary>
    private const int Most = 60;

    /// <summary>The columns to draw against, so a bar lands where its value will.</summary>
    public static readonly StyledProperty<GridColumns?> ColumnsProperty =
        AvaloniaProperty.Register<GridSkeleton, GridColumns?>(nameof(Columns));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<GridSkeleton, IBrush?>(nameof(Fill));

    /// <summary>The rule under each row, which is the body's own.</summary>
    public static readonly StyledProperty<IBrush?> LineProperty =
        AvaloniaProperty.Register<GridSkeleton, IBrush?>(nameof(Line));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<GridSkeleton, double>(nameof(RowHeight), 31);

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<GridSkeleton, double>(nameof(BarHeight), 9);

    /// <summary>How wide a bar is in a column that is not filling the row.</summary>
    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<GridSkeleton, double>(nameof(BarWidth), 34);

    public static readonly StyledProperty<double> BarRadiusProperty =
        AvaloniaProperty.Register<GridSkeleton, double>(nameof(BarRadius), 3);

    /// <summary>The cell's own padding, so a bar starts where a value starts.</summary>
    public static readonly StyledProperty<Thickness> CellPaddingProperty =
        AvaloniaProperty.Register<GridSkeleton, Thickness>(nameof(CellPadding));

    private GridColumns? following;

    static GridSkeleton()
    {
        AffectsRender<GridSkeleton>(
            FillProperty,
            LineProperty,
            RowHeightProperty,
            BarHeightProperty,
            BarWidthProperty,
            BarRadiusProperty,
            CellPaddingProperty);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public GridColumns? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="LineProperty"/>
    public IBrush? Line
    {
        get => GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <inheritdoc cref="BarHeightProperty"/>
    public double BarHeight
    {
        get => GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <inheritdoc cref="BarWidthProperty"/>
    public double BarWidth
    {
        get => GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    /// <inheritdoc cref="BarRadiusProperty"/>
    public double BarRadius
    {
        get => GetValue(BarRadiusProperty);
        set => SetValue(BarRadiusProperty, value);
    }

    /// <inheritdoc cref="CellPaddingProperty"/>
    public Thickness CellPadding
    {
        get => GetValue(CellPaddingProperty);
        set => SetValue(CellPaddingProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var height = RowHeight;

        if (height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var rows = Math.Min(Most, (int)Math.Ceiling(Bounds.Height / height));

        for (var row = 0; row < rows; row++)
        {
            var top = row * height;

            if (Line is { } line)
            {
                context.FillRectangle(line, new Rect(0, top + height - 1, Bounds.Width, 1));
            }

            Draw(context, row, top, height);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ColumnsProperty)
        {
            return;
        }

        // A width is worked out by the frame rather than held here, so the bars are drawn
        // again whenever the columns settle on new ones.
        if (following is not null)
        {
            following.LayoutChanged -= OnColumnsChanged;
        }

        following = change.GetNewValue<GridColumns?>();

        if (following is not null)
        {
            following.LayoutChanged += OnColumnsChanged;
        }

        InvalidateVisual();
    }

    private void OnColumnsChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>One row of bars, one per column that has been given a width.</summary>
    private void Draw(DrawingContext context, int row, double top, double height)
    {
        if (Fill is not { } fill)
        {
            return;
        }

        var padding = CellPadding;
        var tall = Math.Min(BarHeight, height);
        var y = top + ((height - tall) / 2);
        var radius = BarRadius;
        var drawn = 0;

        foreach (var column in Reachable())
        {
            var room = column.ActualWidth - padding.Left - padding.Right;

            if (room <= 1)
            {
                continue;
            }

            // A column that fills the row carries a value of its own length, and a column
            // pinned to a width carries a number, which is the same size every time.
            var wide = column.Width.IsStar
                ? room * Ratios[(row + drawn) % Ratios.Length]
                : Math.Min(room, BarWidth);

            var left = column.Offset + padding.Left + column.Alignment switch
            {
                HorizontalAlignment.Right => room - wide,
                HorizontalAlignment.Center => (room - wide) / 2,
                _ => 0,
            };

            context.DrawRectangle(fill, null, new RoundedRect(new Rect(left, y, wide, tall), radius));
            drawn++;
        }
    }

    private IEnumerable<GridColumn> Reachable() =>
        Columns?.Reachable.Where(column => column.ActualWidth > 0) ?? [];
}
