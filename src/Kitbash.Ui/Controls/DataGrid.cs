using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The flat grid. A list underneath, so it virtualises and selects without any of that
/// being written again, with a column layout over the top.
/// </summary>
public class DataGrid : ListBox
{
    private const string HeaderPart = "PART_Header";
    private const string ScrollerPart = "PART_ScrollViewer";

    /// <summary>Tells a recycled data row from a recycled group header.</summary>
    private static readonly object DataToken = new();
    private static readonly object GroupToken = new();

    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(Toolbar));

    public static readonly DirectProperty<DataGrid, string> SelectionTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(SelectionText), grid => grid.selectionText);

    public static readonly DirectProperty<DataGrid, string> ShownTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(ShownText), grid => grid.shownText);

    public static readonly DirectProperty<DataGrid, string> SortTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(SortText), grid => grid.sortText);

    private readonly GridColumns columns = new();
    private readonly GridFrame frame;

    private GridRows? rows;
    private DataGridCell? editing;
    private object? edited;

    private string selectionText = string.Empty;
    private string shownText = string.Empty;
    private string sortText = string.Empty;

    public DataGrid()
    {
        frame = new GridFrame(this, columns);

        columns.LayoutChanged += (_, _) => frame.Resolve();
        columns.CollectionChanged += (_, _) => frame.Rebuild();

        AddHandler(DoubleTappedEvent, OnDoubleTapped);
        SelectionChanged += (_, _) => UpdateCounts();
    }

    /// <summary>The columns, shared with the header and every row.</summary>
    public GridColumns Columns => columns;

    /// <summary>What sits above the grid. A search field, a column chooser and an action.</summary>
    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    /// <summary>How many rows are picked, or empty when none are.</summary>
    public string SelectionText => selectionText;

    /// <summary>How many rows are drawn out of how many there are.</summary>
    public string ShownText => shownText;

    /// <summary>Which column the sort is on, or empty when the source order stands.</summary>
    public string SortText => sortText;

    /// <summary>The rows this grid was given, or null when it was handed a plain list.</summary>
    public GridRows? Rows => rows;

    /// <summary>The cell being edited, or null when none is.</summary>
    public DataGridCell? EditingCell => editing;

    /// <summary>
    /// Puts a cell into its column's edit template. The item is told through
    /// <see cref="IEditableObject"/> when it implements it, which is what makes cancel
    /// able to put the old value back.
    /// </summary>
    public void BeginEdit(DataGridCell cell)
    {
        if (!cell.CanEdit || ReferenceEquals(cell, editing))
        {
            return;
        }

        CommitEdit();

        editing = cell;
        edited = cell.Content;
        (edited as IEditableObject)?.BeginEdit();

        cell.IsEditing = true;
        cell.FindAncestorOfType<DataGridRow>()?.SetEditing(true);
    }

    /// <summary>Keeps what was typed and closes the editor.</summary>
    public void CommitEdit() => EndEdit(commit: true);

    /// <summary>Puts the old value back and closes the editor.</summary>
    public void CancelEdit() => EndEdit(commit: false);

    /// <summary>Opens a closed group, closes an open one.</summary>
    public void Toggle(GridRow row) => rows?.Toggle(row);

    /// <summary>
    /// Sorts on a column, cycling up, then down, then back to the order the source came
    /// in, so a person can always get back to where they started.
    /// </summary>
    internal void SortBy(GridColumn column)
    {
        if (rows is null || !column.CanSort)
        {
            return;
        }

        var next = ReferenceEquals(rows.SortColumn, column)
            ? rows.SortDirection switch
            {
                GridSortDirection.Ascending => GridSortDirection.Descending,
                GridSortDirection.Descending => GridSortDirection.None,
                _ => GridSortDirection.Ascending,
            }
            : GridSortDirection.Ascending;

        CommitEdit();
        rows.Sort(column, next);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = item is GridRow { IsGroup: true } ? GroupToken : DataToken;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        ReferenceEquals(recycleKey, GroupToken) ? new GridGroupRow() : new DataGridRow();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        Follow(container, item as GridRow);
    }

    /// <summary>
    /// A container kept but moved. Everything it was told is read again from whatever row
    /// now sits at its index, since this is the one place a recycled container is reused
    /// without being cleared first.
    /// </summary>
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        Follow(container, newIndex >= 0 && newIndex < ItemsView.Count ? ItemsView[newIndex] as GridRow : null);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        Follow(container, null);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        frame.Attach(
            e.NameScope.Find<DataGridHeader>(HeaderPart),
            e.NameScope.Find<ScrollViewer>(ScrollerPart));

        UpdateCounts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            if (rows is not null)
            {
                rows.Rebuilt -= OnRowsRebuilt;
            }

            rows = ItemsSource as GridRows;

            if (rows is not null)
            {
                rows.Rebuilt += OnRowsRebuilt;
            }

            CancelEdit();
            UpdateCounts();
        }
    }

    private void Follow(Control container, GridRow? row)
    {
        switch (container)
        {
            case DataGridRow data:
                data.Attach(columns);
                data.SetEditing(false);
                data.Follow(row);
                break;

            case GridGroupRow group:
                group.Follow(row);
                break;
        }
    }

    private void EndEdit(bool commit)
    {
        if (editing is null)
        {
            return;
        }

        var cell = editing;
        var item = edited;

        editing = null;
        edited = null;

        if (commit)
        {
            (item as IEditableObject)?.EndEdit();
        }
        else
        {
            (item as IEditableObject)?.CancelEdit();
        }

        cell.IsEditing = false;
        cell.FindAncestorOfType<DataGridRow>()?.SetEditing(false);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source
            && source.FindAncestorOfType<DataGridCell>(includeSelf: true) is { CanEdit: true } cell)
        {
            BeginEdit(cell);
            e.Handled = true;
        }
    }

    private void OnRowsRebuilt(object? sender, EventArgs e)
    {
        CancelEdit();
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var picked = SelectedItems?.Count ?? 0;

        SetAndRaise(SelectionTextProperty, ref selectionText, picked == 0 ? string.Empty : $"{picked:N0} selected");

        SetAndRaise(
            ShownTextProperty,
            ref shownText,
            rows is null ? string.Empty : $"{rows.Shown:N0} of {rows.Total:N0} shown");

        SetAndRaise(
            SortTextProperty,
            ref sortText,
            rows?.SortColumn is { } column ? $"sorted by {column.Header}" : string.Empty);
    }
}
