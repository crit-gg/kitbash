using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// One column title. Clicking a sortable one cycles up, down and back to the source order.
/// </summary>
public class DataGridHeaderCell : ContentControl
{
    public static readonly StyledProperty<GridColumn?> ColumnProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, GridColumn?>(nameof(Column));

    private IDisposable? following;

    public GridColumn? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ColumnProperty)
        {
            return;
        }

        following?.Dispose();
        following = null;

        var column = change.GetNewValue<GridColumn?>();

        Content = column?.Header;
        HorizontalContentAlignment = column?.Alignment ?? Avalonia.Layout.HorizontalAlignment.Stretch;
        PseudoClasses.Set(":sortable", column?.CanSort == true);

        if (column is null)
        {
            Apply(GridSortDirection.None);
            return;
        }

        following = column.GetObservable(GridColumn.SortDirectionProperty)
            .Subscribe(new AnonymousObserver<GridSortDirection>(Apply));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (Column is { CanSort: true } column
            && e.InitialPressMouseButton == MouseButton.Left
            && this.FindAncestorOfType<DataGrid>() is { } grid)
        {
            grid.SortBy(column);
            e.Handled = true;
        }
    }

    private void Apply(GridSortDirection direction)
    {
        PseudoClasses.Set(":sorted", direction != GridSortDirection.None);
        PseudoClasses.Set(":ascending", direction == GridSortDirection.Ascending);
        PseudoClasses.Set(":descending", direction == GridSortDirection.Descending);
    }
}
