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

    /// <summary>How far sideways a press has to travel before it is a drag and not a click.</summary>
    private const double Slack = 4;

    private IDisposable? following;
    private IDisposable? ordering;
    private Point? from;
    private bool moving;

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

        var moved = moving;

        from = null;
        moving = false;
        PseudoClasses.Set(":moving", false);

        if (moved)
        {
            this.FindAncestorOfType<DataGridHeader>()?.EndMove();
            e.Handled = true;
            return;
        }

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

        if (e.Source is Visual source
            && source.FindAncestorOfType<Border>(includeSelf: true) is { Name: MenuPart })
        {
            Open();
            e.Handled = true;
            return;
        }

        if (this.FindAncestorOfType<DataGridHeader>() is { } strip
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            from = e.GetPosition(strip);
        }
    }

    /// <summary>
    /// A title dragged sideways moves its column. It only starts once the pointer has gone
    /// far enough that it cannot be the shake of a click, or every sort would move a column.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Column is not { } column
            || from is not { } start
            || this.FindAncestorOfType<DataGridHeader>() is not { } strip
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var here = e.GetPosition(strip);

        if (!moving && Math.Abs(here.X - start.X) < Slack)
        {
            return;
        }

        if (!moving)
        {
            moving = true;
            strip.BeginMove(column);
            PseudoClasses.Set(":moving", true);
        }

        strip.MoveOver(here.X);
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

        if (host.Columns.CanPin(column))
        {
            items.Add(column.IsPinned
                ? Item("Unpin column", () => column.IsPinned = false)
                : Item("Pin to the left", () => column.IsPinned = true));
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
        && (column.CanSort
            || (host.CanGroup && column.HasValue)
            || host.Columns.CanPin(column)
            || host.Columns.CanHide(column));

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
