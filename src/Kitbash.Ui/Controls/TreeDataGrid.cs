using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The tree grid. The tree from stage nine with the flat grid's columns over it, so the
/// hierarchy, the flat row list and the column widths are each written once.
/// </summary>
public class TreeDataGrid : Tree, IGridSorting
{
    private const string HeaderPart = "PART_Header";
    private const string ScrollerPart = "PART_ScrollViewer";

    /// <summary>
    /// The column the hierarchy indents in. The first column when unset, which is right
    /// only when nothing stands in front of the names.
    /// </summary>
    public static readonly StyledProperty<GridColumn?> LeadColumnProperty =
        AvaloniaProperty.Register<TreeDataGrid, GridColumn?>(nameof(LeadColumn));

    private readonly GridColumns columns = new();
    private readonly GridFrame frame;
    private readonly GridValueComparer order = new();

    private GridColumn? sorted;

    public TreeDataGrid()
    {
        frame = new GridFrame(this, columns);

        columns.LayoutChanged += (_, _) => frame.Resolve();
        columns.CollectionChanged += (_, _) => frame.Rebuild();
    }

    /// <summary>The columns, shared with the header and every row.</summary>
    public GridColumns Columns => columns;

    public GridColumn? LeadColumn
    {
        get => GetValue(LeadColumnProperty);
        set => SetValue(LeadColumnProperty, value);
    }

    /// <summary>
    /// Sorts siblings under every parent, which is what sorting a hierarchy means. A tree
    /// closes when it is sorted, since the rows are built again from the roots.
    /// </summary>
    void IGridSorting.SortBy(GridColumn column) => SortBy(column);

    internal void SortBy(GridColumn column)
    {
        if (ItemsSource is not TreeRows tree || column.SortKey is not { } key)
        {
            return;
        }

        var next = column.NextSort();

        if (sorted is { } old && !ReferenceEquals(old, column))
        {
            old.SortDirection = GridSortDirection.None;
        }

        column.SortDirection = next;
        sorted = next == GridSortDirection.None ? null : column;

        tree.Sort(next == GridSortDirection.None
            ? null
            : new GridKeyComparer(key, column.Comparer ?? order, next == GridSortDirection.Descending));
    }

    protected override TreeItem CreateRow() => new TreeDataGridRow();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (container is TreeDataGridRow row)
        {
            // Before the base call, so the cells are there for the row to fill.
            row.Attach(columns, LeadColumn);
        }

        base.PrepareContainerForItemOverride(container, item, index);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        frame.Attach(
            e.NameScope.Find<DataGridHeader>(HeaderPart),
            e.NameScope.Find<ScrollViewer>(ScrollerPart));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != LeadColumnProperty)
        {
            return;
        }

        foreach (var container in GetRealizedContainers())
        {
            (container as TreeDataGridRow)?.Attach(columns, LeadColumn);
        }
    }
}
