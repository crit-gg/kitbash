using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One column title. Clicking a sortable one cycles up, down and back to the source order.
/// </summary>
public class DataGridHeaderCell : ContentControl
{
    public static readonly StyledProperty<GridColumn?> ColumnProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, GridColumn?>(nameof(Column));

    /// <summary>Which key this column is in a sort of more than one, as it is drawn.</summary>
    public static readonly StyledProperty<string> OrdinalProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, string>(nameof(Ordinal), string.Empty);

    public static readonly StyledProperty<bool> HasOrdinalProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, bool>(nameof(HasOrdinal));

    private const string MenuPart = "PART_Menu";

    private IDisposable? following;
    private IDisposable? ordering;

    public GridColumn? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    /// <inheritdoc cref="OrdinalProperty"/>
    public string Ordinal
    {
        get => GetValue(OrdinalProperty);
        set => SetValue(OrdinalProperty, value);
    }

    /// <inheritdoc cref="HasOrdinalProperty"/>
    public bool HasOrdinal
    {
        get => GetValue(HasOrdinalProperty);
        set => SetValue(HasOrdinalProperty, value);
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
        ordering?.Dispose();
        ordering = null;

        var column = change.GetNewValue<GridColumn?>();

        Content = column?.Header;
        HorizontalContentAlignment = column?.Alignment ?? Avalonia.Layout.HorizontalAlignment.Stretch;
        PseudoClasses.Set(":sortable", column?.CanSort == true);
        PseudoClasses.Set(":menued", Offers(column));

        if (column is null)
        {
            Apply(GridSortDirection.None);
            return;
        }

        following = column.GetObservable(GridColumn.SortDirectionProperty)
            .Subscribe(new AnonymousObserver<GridSortDirection>(Apply));

        ordering?.Dispose();
        ordering = column.GetObservable(GridColumn.SortOrderProperty)
            .Subscribe(new AnonymousObserver<int>(Number));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // Asked of whatever owns the columns rather than of a named grid. Naming DataGrid
        // here is what left a tree grid header showing a hand cursor and doing nothing.
        if (Column is { CanSort: true } column
            && e.InitialPressMouseButton == MouseButton.Left
            && this.FindAncestorOfType<IGridSorting>() is { } grid)
        {
            grid.SortBy(column, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        }
    }

    /// <summary>
    /// The chevron opens the menu and the rest of the title sorts, so the common action
    /// never costs a menu.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Source is not Visual source
            || source.FindAncestorOfType<Border>(includeSelf: true) is not { Name: MenuPart })
        {
            return;
        }

        Open();
        e.Handled = true;
    }

    /// <summary>
    /// What the menu offers, which is what the grid and the column between them allow. An
    /// item nothing can answer is left out rather than shown and disabled.
    /// </summary>
    private void Open()
    {
        if (Column is not { } column || this.FindAncestorOfType<IGridHost>() is not { } host)
        {
            return;
        }

        var items = new List<Control>();

        if (column.CanSort)
        {
            items.Add(Item("Sort ascending", () => host.SetSort(column, GridSortDirection.Ascending)));
            items.Add(Item("Sort descending", () => host.SetSort(column, GridSortDirection.Descending)));

            if (column.SortDirection != GridSortDirection.None)
            {
                items.Add(Item("Clear sort on this column", () => host.SetSort(column, GridSortDirection.None)));
            }
        }

        if (host.CanGroup && column.HasValue)
        {
            items.Add(Item("Group by this column", () => host.GroupBy(column)));
        }

        if (host.Columns.CanHide(column))
        {
            items.Add(Item("Hide column", () => column.IsVisible = false));
        }

        if (items.Count == 0)
        {
            return;
        }

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };

        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => PseudoClasses.Set(":menu", false);

        PseudoClasses.Set(":menu", true);
        menu.ShowAt(this);
    }

    private static MenuItem Item(string label, Action act) =>
        new() { Header = label, Command = new TemplateCommand(_ => act()) };

    /// <summary>Whether the menu would have anything in it, so no chevron promises nothing.</summary>
    private bool Offers(GridColumn? column) =>
        column is not null
        && this.FindAncestorOfType<IGridHost>() is { } host
        && (column.CanSort || (host.CanGroup && column.HasValue) || host.Columns.CanHide(column));

    private void Number(int order)
    {
        SetCurrentValue(OrdinalProperty, order > 0 ? order.ToString("N0") : string.Empty);
        SetCurrentValue(HasOrdinalProperty, order > 0);
    }

    private void Apply(GridSortDirection direction)
    {
        PseudoClasses.Set(":sorted", direction != GridSortDirection.None);
        PseudoClasses.Set(":ascending", direction == GridSortDirection.Ascending);
        PseudoClasses.Set(":descending", direction == GridSortDirection.Descending);
    }
}
