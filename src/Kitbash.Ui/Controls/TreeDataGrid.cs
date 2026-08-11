using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The tree grid. The tree from stage nine with the flat grid's columns over it, so the
/// hierarchy, the flat row list and the column widths are each written once.
/// </summary>
public class TreeDataGrid : Tree, IGridHost
{
    private const string HeaderPart = "PART_Header";
    private const string ScrollerPart = "PART_ScrollViewer";

    /// <summary>
    /// The column the hierarchy indents in. The first column when unset, which is right
    /// only when nothing stands in front of the names.
    /// </summary>
    public static readonly StyledProperty<GridColumn?> LeadColumnProperty =
        AvaloniaProperty.Register<TreeDataGrid, GridColumn?>(nameof(LeadColumn));

    /// <summary>
    /// The flat grid declares it and this one takes it on, so the switch is written once
    /// and both grids answer the same markup.
    /// </summary>
    public static readonly StyledProperty<BeginEditGestures> BeginEditGesturesProperty =
        DataGrid.BeginEditGesturesProperty.AddOwner<TreeDataGrid>();

    private readonly GridColumns columns = new();
    private readonly GridFrame frame;
    private readonly GridBody body;
    private readonly GridValueComparer order = new();

    private GridColumn? sorted;

    public TreeDataGrid()
    {
        frame = new GridFrame(this, columns);
        body = new GridBody(this, columns);

        columns.LayoutChanged += (_, _) => frame.Resolve();
        columns.CollectionChanged += (_, _) =>
        {
            frame.Rebuild();
            body.Refresh();
        };

        SelectionChanged += (_, _) => body.Refresh();
    }

    /// <inheritdoc cref="DataGrid.BeginEditGesturesProperty"/>
    public BeginEditGestures BeginEditGestures
    {
        get => GetValue(BeginEditGesturesProperty);
        set => SetValue(BeginEditGesturesProperty, value);
    }

    /// <summary>The columns, shared with the header and every row.</summary>
    public GridColumns Columns => columns;

    /// <summary>The cell being edited, or null when none is.</summary>
    public DataGridCell? EditingCell => body.EditingCell;

    /// <inheritdoc cref="GridBody.BeginEdit"/>
    public void BeginEdit(DataGridCell cell) => body.BeginEdit(cell);

    /// <summary>Keeps what was typed and closes the editor.</summary>
    public void CommitEdit() => body.CommitEdit();

    /// <summary>Puts the old value back and closes the editor.</summary>
    public void CancelEdit() => body.CancelEdit();

    GridBody IGridHost.Body => body;

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

        body.CommitEdit();

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

    /// <summary>A double click in a cell that can be edited opens it rather than the row.</summary>
    protected override bool Claimed(Visual source) => body.BeginEditAt(source);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        Release(container);

        if (container is TreeDataGridRow row)
        {
            // Before the base call, so the cells are there for the row to fill.
            row.Attach(columns, LeadColumn);
        }

        base.PrepareContainerForItemOverride(container, item, index);
        body.Refresh();
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        Release(container);
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        body.Refresh();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        Release(container);
        base.ClearContainerForItemOverride(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        frame.Attach(
            e.NameScope.Find<DataGridHeader>(HeaderPart),
            e.NameScope.Find<ScrollViewer>(ScrollerPart));
    }

    /// <summary>
    /// The cell keys are answered before the hierarchy and the list, since Home and End mean
    /// the ends of a row here. Left and right are the exception: the body declines them in
    /// the column that draws the caret, so they reach the tree and open the row.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        body.Hierarchy = LeadColumn ?? columns.Reachable.FirstOrDefault();

        if (!e.Handled && body.OnKeyDown(e))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        body.OnTextInput(e);
        base.OnTextInput(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BeginEditGesturesProperty)
        {
            body.Gestures = change.GetNewValue<BeginEditGestures>();
            return;
        }

        if (change.Property != LeadColumnProperty)
        {
            return;
        }

        foreach (var container in GetRealizedContainers())
        {
            (container as TreeDataGridRow)?.Attach(columns, LeadColumn);
        }
    }

    /// <summary>
    /// Closes an editor in a container that is about to stand for another row. Without it
    /// the grid holds a cell that has moved on and the editor shows one row's field over
    /// another row's data.
    /// </summary>
    private void Release(Control container)
    {
        if (body.Holds(container))
        {
            body.CommitEdit();
        }
    }
}
