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
    private const string SkeletonPart = "PART_Skeleton";

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

    /// <inheritdoc cref="BeginEditGesturesProperty"/>
    public static readonly StyledProperty<GridEditUnit> EditUnitProperty =
        DataGrid.EditUnitProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="BeginEditGesturesProperty"/>
    public static readonly StyledProperty<CellActions> CellActionsProperty =
        DataGrid.CellActionsProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="BeginEditGesturesProperty"/>
    public static readonly StyledProperty<GridSelectionUnit> SelectionUnitProperty =
        DataGrid.SelectionUnitProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="BeginEditGesturesProperty"/>
    public static readonly StyledProperty<ColumnGestures> ColumnGesturesProperty =
        DataGrid.ColumnGesturesProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="DataGrid.EmptyContentProperty"/>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        DataGrid.EmptyContentProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="DataGrid.NoMatchesContentProperty"/>
    public static readonly StyledProperty<object?> NoMatchesContentProperty =
        DataGrid.NoMatchesContentProperty.AddOwner<TreeDataGrid>();

    /// <inheritdoc cref="DataGrid.IsLoadingProperty"/>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        DataGrid.IsLoadingProperty.AddOwner<TreeDataGrid>();

    private readonly GridColumns columns = new();
    private readonly GridFrame frame;
    private readonly GridBody body;
    private readonly GridValueComparer order = new();

    private GridColumn? sorted;
    private TreeRows? tree;

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

        SelectionChanged += (_, _) =>
        {
            body.OnSelectionMoved();
            body.Refresh();
        };
    }

    /// <inheritdoc cref="DataGrid.BeginEditGesturesProperty"/>
    public BeginEditGestures BeginEditGestures
    {
        get => GetValue(BeginEditGesturesProperty);
        set => SetValue(BeginEditGesturesProperty, value);
    }

    /// <inheritdoc cref="GridEditUnit"/>
    public GridEditUnit EditUnit
    {
        get => GetValue(EditUnitProperty);
        set => SetValue(EditUnitProperty, value);
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

    /// <inheritdoc cref="ColumnGestures"/>
    public ColumnGestures ColumnGestures
    {
        get => GetValue(ColumnGesturesProperty);
        set => SetValue(ColumnGesturesProperty, value);
    }

    /// <inheritdoc cref="GridSelectionUnit"/>
    public GridSelectionUnit SelectionUnit
    {
        get => GetValue(SelectionUnitProperty);
        set => SetValue(SelectionUnitProperty, value);
    }

    /// <inheritdoc cref="CellActions"/>
    public CellActions CellActions
    {
        get => GetValue(CellActionsProperty);
        set => SetValue(CellActionsProperty, value);
    }

    /// <inheritdoc cref="DataGrid.EmptyContentProperty"/>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    /// <inheritdoc cref="DataGrid.NoMatchesContentProperty"/>
    public object? NoMatchesContent
    {
        get => GetValue(NoMatchesContentProperty);
        set => SetValue(NoMatchesContentProperty, value);
    }

    /// <inheritdoc cref="DataGrid.IsLoadingProperty"/>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Which rows hold changes that are not saved, which a tool alone knows. Null marks
    /// none. It is read again whenever an item says one of its properties changed.
    /// </summary>
    public Func<object, bool>? RowModified
    {
        get => body.Modified;
        set
        {
            body.Modified = value;
            body.Refresh();
        }
    }

    /// <inheritdoc cref="GridBody.CommitRow"/>
    public void CommitRowEdit() => body.CommitRow();

    /// <inheritdoc cref="GridBody.CancelRow"/>
    public void CancelRowEdit() => body.CancelRow();

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
    // A hierarchy is already a grouping, so there is nothing here for a second one to mean.
    bool IGridHost.CanGroup => false;

    void IGridHost.GroupBy(GridColumn column)
    {
    }

    void IGridSorting.SetSort(GridColumn column, GridSortDirection direction)
    {
        if (column.SortDirection != direction)
        {
            // The tree only cycles, so it is stepped round until it lands on what was asked.
            for (var step = 0; step < 3 && column.SortDirection != direction; step++)
            {
                SortBy(column);
            }
        }
    }

    // A tree sorts siblings under every parent, and a second key over a hierarchy has no
    // meaning the design has settled, so a shift click here is an ordinary one.
    void IGridSorting.SortBy(GridColumn column, bool adds) => SortBy(column);

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
        Mark(container);
        body.Refresh();
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        Release(container);
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        Mark(container);
        body.Refresh();
    }

    /// <summary>
    /// Reads one container's marks off its item. By name, since the sweep only reaches the
    /// containers the panel has published and a row being prepared is not one of them.
    /// </summary>
    private void Mark(Control container)
    {
        if (container is IGridRowLayout row)
        {
            body.Mark(row);
        }
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

        // The skeleton draws a bar where each value will be, so it needs the same widths
        // the rows are laid out against.
        if (e.NameScope.Find<GridSkeleton>(SkeletonPart) is { } skeleton)
        {
            skeleton.Columns = columns;
        }

        ShowEmpty();
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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        body.EndFill();
        body.EndDrag();
        base.OnPointerReleased(e);
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
            body.Refresh();
            return;
        }

        if (change.Property == EditUnitProperty)
        {
            body.CommitRow();
            body.Unit = change.GetNewValue<GridEditUnit>();
            return;
        }

        if (change.Property == CellActionsProperty)
        {
            body.Actions = change.GetNewValue<CellActions>();
            body.Refresh();
            return;
        }

        if (change.Property == ColumnGesturesProperty)
        {
            columns.Gestures = change.GetNewValue<ColumnGestures>();
            frame.Rebuild();
            return;
        }

        if (change.Property == SelectionUnitProperty)
        {
            body.Selection = change.GetNewValue<GridSelectionUnit>();
            body.Refresh();
            return;
        }

        if (change.Property == IsLoadingProperty)
        {
            ShowEmpty();
            return;
        }

        if (change.Property == ItemsSourceProperty)
        {
            Follow(change.GetNewValue<System.Collections.IEnumerable?>() as TreeRows);
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
    /// Watches the rows themselves, since expanding, collapsing, sorting and filtering all
    /// change how many there are without the source being handed over again.
    /// </summary>
    private void Follow(TreeRows? rows)
    {
        if (tree is not null)
        {
            tree.CollectionChanged -= OnRowsChanged;
        }

        tree = rows;

        if (tree is not null)
        {
            tree.CollectionChanged += OnRowsChanged;
        }

        ShowEmpty();
    }

    private void OnRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        ShowEmpty();

    /// <summary>
    /// Which of the three empty states is on, if any. A tree tells an empty one from an
    /// emptied one by whether a filter is on, since a filter is the only thing that takes
    /// rows away without the source changing.
    /// </summary>
    private void ShowEmpty()
    {
        // The rows themselves rather than ItemCount, so this never depends on whether the
        // items view heard the same change first.
        var loading = IsLoading;
        var bare = !loading && (tree?.Count ?? ItemCount) == 0;

        PseudoClasses.Set(":loading", loading);
        PseudoClasses.Set(":nomatches", bare && tree is { IsFiltered: true });
        PseudoClasses.Set(":nothing", bare && tree is not { IsFiltered: true });
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
