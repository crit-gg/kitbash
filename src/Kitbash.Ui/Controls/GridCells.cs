using Avalonia;
using Avalonia.Controls;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Lays one element out per column, at the widths <see cref="GridColumns"/> worked out. A
/// row, a header and a tree grid row all use it, which is what keeps the three in step.
/// </summary>
public class GridCells : Panel
{
    /// <summary>Which column an element belongs to. An element with none is not laid out.</summary>
    public static readonly AttachedProperty<GridColumn?> ColumnProperty =
        AvaloniaProperty.RegisterAttached<GridCells, Control, GridColumn?>("Column");

    /// <summary>
    /// This element straddles the column's trailing edge rather than filling the column.
    /// It is how a resize thumb reaches into both sides without being one column wide.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDividerProperty =
        AvaloniaProperty.RegisterAttached<GridCells, Control, bool>("IsDivider");

    /// <summary>How wide a divider is. The theme sets it from the splitter's reach.</summary>
    public static readonly StyledProperty<double> DividerReachProperty =
        AvaloniaProperty.Register<GridCells, double>(nameof(DividerReach), 7d);

    /// <summary>
    /// How far the body has scrolled sideways. A pinned column is arranged this much further
    /// along, which is exactly what holds it still while the rest travel under it.
    /// </summary>
    public static readonly StyledProperty<double> PinOffsetProperty =
        AvaloniaProperty.Register<GridCells, double>(nameof(PinOffset));

    static GridCells()
    {
        AffectsParentMeasure<GridCells>(ColumnProperty, IsDividerProperty);
        AffectsMeasure<GridCells>(DividerReachProperty);
        AffectsArrange<GridCells>(PinOffsetProperty);
    }

    public double DividerReach
    {
        get => GetValue(DividerReachProperty);
        set => SetValue(DividerReachProperty, value);
    }

    /// <inheritdoc cref="PinOffsetProperty"/>
    public double PinOffset
    {
        get => GetValue(PinOffsetProperty);
        set => SetValue(PinOffsetProperty, value);
    }

    public static GridColumn? GetColumn(Control control) => control.GetValue(ColumnProperty);

    public static void SetColumn(Control control, GridColumn? value) => control.SetValue(ColumnProperty, value);

    public static bool GetIsDivider(Control control) => control.GetValue(IsDividerProperty);

    public static void SetIsDivider(Control control, bool value) => control.SetValue(IsDividerProperty, value);

    /// <summary>
    /// The whole column set's width, whatever the constraint. Returning less would let a
    /// narrow viewport squeeze the columns instead of scrolling to them.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        var height = 0d;

        foreach (var child in Children)
        {
            var column = GetColumn(child);

            if (column is null)
            {
                child.Measure(default);
                continue;
            }

            var reach = GetIsDivider(child) ? DividerReach : column.ActualWidth;

            child.Measure(new Size(reach, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);

            if (!GetIsDivider(child))
            {
                width = Math.Max(width, column.Offset + column.ActualWidth);
            }
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var column = GetColumn(child);

            if (column is null)
            {
                child.Arrange(default);
                continue;
            }

            // A pinned column travels with the scroll, so it stays where it was drawn, and
            // it draws over the columns going under it.
            var held = column.IsPinned ? PinOffset : 0;

            child.ZIndex = column.IsPinned ? 1 : 0;

            if (GetIsDivider(child))
            {
                var reach = DividerReach;
                var edge = column.Offset + column.ActualWidth + held;

                child.Arrange(new Rect(edge - reach / 2, 0, reach, finalSize.Height));
            }
            else
            {
                child.Arrange(new Rect(column.Offset + held, 0, column.ActualWidth, finalSize.Height));
            }
        }

        return finalSize;
    }
}
