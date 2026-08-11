using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The flat grid. A list underneath, so it virtualises and selects without any of that
/// being written again, with a column layout over the top.
/// </summary>
public class DataGrid : ListBox, IGridHost
{
    private const string HeaderPart = "PART_Header";
    private const string ScrollerPart = "PART_ScrollViewer";
    private const string SeamPart = "PART_Seam";
    private const string ActionBarPart = "PART_ActionBar";

    /// <summary>Tells a recycled data row from a recycled group header.</summary>
    private static readonly object DataToken = new();
    private static readonly object GroupToken = new();

    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(Toolbar));

    /// <summary>
    /// What a grid that has never had anything says. One sentence and one action, since it
    /// is the only empty state that teaches.
    /// </summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(EmptyContent));

    /// <summary>
    /// What a grid emptied by its filter says. Only the caller knows what was typed, so
    /// only the caller can repeat it beside the way to clear it.
    /// </summary>
    public static readonly StyledProperty<object?> NoMatchesContentProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(NoMatchesContent));

    /// <summary>Whether the rows are still being fetched. Skeleton rows, never a spinner.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsLoading));

    /// <summary>
    /// What a person can do to every picked row at once. Null leaves the bar out, so a grid
    /// that has nothing to do with a set never grows one.
    /// </summary>
    public static readonly StyledProperty<object?> SelectionActionsProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(SelectionActions));

    /// <summary>What the action bar says it holds, or empty while the bar is down.</summary>
    public static readonly DirectProperty<DataGrid, string> PickedTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(PickedText), grid => grid.pickedText);

    public static readonly DirectProperty<DataGrid, string> SelectionTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(SelectionText), grid => grid.selectionText);

    public static readonly DirectProperty<DataGrid, string> ShownTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(ShownText), grid => grid.shownText);

    public static readonly DirectProperty<DataGrid, string> SortTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(SortText), grid => grid.sortText);

    /// <summary>Where the pinned columns end, which is where the seam falls.</summary>
    public static readonly DirectProperty<DataGrid, double> PinnedWidthProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, double>(nameof(PinnedWidth), grid => grid.pinnedWidth);

    /// <summary>Whether anything has gone under the pinned columns yet.</summary>
    public static readonly DirectProperty<DataGrid, bool> IsPinnedScrolledProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, bool>(nameof(IsPinnedScrolled), grid => grid.pinnedScrolled);

    /// <summary>What is blocking a save, or empty when nothing is.</summary>
    public static readonly DirectProperty<DataGrid, string> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, string>(nameof(ErrorText), grid => grid.errorText);

    public static readonly StyledProperty<BeginEditGestures> BeginEditGesturesProperty =
        AvaloniaProperty.Register<DataGrid, BeginEditGestures>(
            nameof(BeginEditGestures),
            BeginEditGestures.DoubleTap | BeginEditGestures.F2 | BeginEditGestures.Enter);

    public static readonly StyledProperty<GridEditUnit> EditUnitProperty =
        AvaloniaProperty.Register<DataGrid, GridEditUnit>(nameof(EditUnit));

    public static readonly StyledProperty<CellActions> CellActionsProperty =
        AvaloniaProperty.Register<DataGrid, CellActions>(nameof(CellActions), CellActions.Copy);

    public static readonly StyledProperty<GridSelectionUnit> SelectionUnitProperty =
        AvaloniaProperty.Register<DataGrid, GridSelectionUnit>(nameof(SelectionUnit));

    public static readonly StyledProperty<ColumnGestures> ColumnGesturesProperty =
        AvaloniaProperty.Register<DataGrid, ColumnGestures>(
            nameof(ColumnGestures),
            ColumnGestures.Resize | ColumnGestures.FitToContents);

    private readonly GridColumns columns = new();
    private readonly GridFrame frame;
    private readonly GridBody body;

    private GridRows? rows;
    private GridColumn? grouped;
    private bool dropping;

    /// <summary>What was picked and where the body sat, held across one rebuild.</summary>
    private List<GridRow>? held;
    private Vector resting;

    /// <summary>Which page was last drawn, so a turn can be told from any other rebuild.</summary>
    private int page = 1;

    private Border? seam;
    private GridSelectionBar? bar;
    private double pinnedWidth;
    private bool pinnedScrolled;
    private string selectionText = string.Empty;
    private string pickedText = string.Empty;
    private string shownText = string.Empty;
    private string sortText = string.Empty;
    private string errorText = string.Empty;

    static DataGrid()
    {
        // A grid's rows are wrappers, so SelectedItem is a GridRow and binding it to a
        // typed property never lands. SelectedValue is Avalonia's own answer and it
        // unwraps both ways once it is told where the item is.
        SelectedValueBindingProperty.OverrideDefaultValue<DataGrid>(new Binding(nameof(GridRow.Item)));
    }

    public DataGrid()
    {
        frame = new GridFrame(this, columns);
        body = new GridBody(this, columns);

        columns.LayoutChanged += (_, _) => frame.Resolve();
        columns.CollectionChanged += (_, _) =>
        {
            frame.Rebuild();
            body.Refresh();
        };

        body.RangeChanged += (_, _) => UpdateCounts();
        body.ErrorsChanged += (_, _) => UpdateCounts();

        frame.Seam += (_, scrolled) => ShowSeam(scrolled);

        AddHandler(DoubleTappedEvent, OnDoubleTapped);

        SelectionChanged += (_, _) =>
        {
            DropHeadings();
            UpdateCounts();
            body.OnSelectionMoved();
            body.Refresh();
        };
    }

    /// <inheritdoc cref="BeginEditGesturesProperty"/>
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

    /// <summary>What sits above the grid. A search field, a column chooser and an action.</summary>
    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    /// <inheritdoc cref="EmptyContentProperty"/>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    /// <inheritdoc cref="NoMatchesContentProperty"/>
    public object? NoMatchesContent
    {
        get => GetValue(NoMatchesContentProperty);
        set => SetValue(NoMatchesContentProperty, value);
    }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <inheritdoc cref="SelectionActionsProperty"/>
    public object? SelectionActions
    {
        get => GetValue(SelectionActionsProperty);
        set => SetValue(SelectionActionsProperty, value);
    }

    /// <inheritdoc cref="PickedTextProperty"/>
    public string PickedText => pickedText;

    /// <summary>How many rows are picked, or empty when none are or the bar has the count.</summary>
    public string SelectionText => selectionText;

    /// <summary>How many rows are drawn out of how many there are.</summary>
    public string ShownText => shownText;

    /// <summary>Which column the sort is on, or empty when the source order stands.</summary>
    public string SortText => sortText;

    /// <inheritdoc cref="PinnedWidthProperty"/>
    public double PinnedWidth => pinnedWidth;

    /// <inheritdoc cref="IsPinnedScrolledProperty"/>
    public bool IsPinnedScrolled => pinnedScrolled;

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string ErrorText => errorText;

    /// <summary>The rows this grid was given, or null when it was handed a plain list.</summary>
    public GridRows? Rows => rows;

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

    /// <summary>
    /// Counts what is blocking a save again, over every row and not only the ones on screen.
    /// A row nobody has scrolled to has no container to hear its errors change, so a tool
    /// that has just checked its whole set says so here.
    /// </summary>
    public void Revalidate()
    {
        body.Recount();
        UpdateCounts();
    }

    /// <inheritdoc cref="GridBody.CommitRow"/>
    public void CommitRowEdit() => body.CommitRow();

    /// <inheritdoc cref="GridBody.CancelRow"/>
    public void CancelRowEdit() => body.CancelRow();

    GridBody IGridHost.Body => body;

    /// <summary>Opens a closed group, closes an open one.</summary>
    public void Toggle(GridRow row) => rows?.Toggle(row);

    /// <summary>
    /// Sorts on a column, cycling up, then down, then back to the order the source came
    /// in, so a person can always get back to where they started.
    /// </summary>
    bool IGridHost.CanGroup => true;

    /// <summary>
    /// Groups on a column's own value, or drops the grouping when it is already the one
    /// grouped by, so the menu item toggles rather than only ever turning it on.
    /// </summary>
    void IGridHost.GroupBy(GridColumn column)
    {
        if (rows is null)
        {
            return;
        }

        CommitEdit();

        var same = ReferenceEquals(grouped, column);

        grouped = same ? null : column;
        rows.Group(same ? null : item => column.ValueOf(item));
    }

    void IGridSorting.SetSort(GridColumn column, GridSortDirection direction)
    {
        if (rows is null || !column.CanSort)
        {
            return;
        }

        CommitEdit();
        rows.Sort(direction == GridSortDirection.None ? null : column, direction);
    }

    void IGridSorting.SortBy(GridColumn column, bool adds) => SortBy(column, adds);

    internal void SortBy(GridColumn column, bool adds = false)
    {
        if (rows is null || !column.CanSort)
        {
            return;
        }

        CommitEdit();

        if (adds && columns.Gestures.HasFlag(ColumnGestures.MultiSort))
        {
            rows.AddSort(column, column.NextSort());
            return;
        }

        rows.Sort(column, column.NextSort());
    }

    /// <summary>
    /// Arrowing past a heading rather than onto one. Taken only when the next row is a
    /// heading and nothing is held down, so extending a selection is still the list's own.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var by = e.Key switch
        {
            Key.Down => 1,
            Key.Up => -1,
            _ => 0,
        };

        if (!e.Handled && by != 0 && e.KeyModifiers == KeyModifiers.None && IsHeading(SelectedIndex + by))
        {
            var next = SelectedIndex + by;

            while (IsHeading(next))
            {
                next += by;
            }

            if (next >= 0 && next < ItemsView.Count)
            {
                SelectedIndex = next;
                e.Handled = true;
                return;
            }
        }

        // Only while the bar owns the selection. A grid inside a dialog has to leave Escape
        // to the dialog, and taking it whenever a row is picked would close nothing.
        if (!e.Handled && e.Key == Key.Escape && EditingCell is null && pickedText.Length > 0)
        {
            DropSelection();
            e.Handled = true;
            return;
        }

        // Before the list, since Home and End mean the ends of a row here rather than the
        // ends of the list, and the list would take them first.
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

        seam = e.NameScope.Find<Border>(SeamPart);

        if (bar is not null)
        {
            bar.Dropped -= OnSelectionDropped;
        }

        bar = e.NameScope.Find<GridSelectionBar>(ActionBarPart);

        if (bar is not null)
        {
            bar.Dropped += OnSelectionDropped;
        }

        frame.Attach(
            e.NameScope.Find<DataGridHeader>(HeaderPart),
            e.NameScope.Find<ScrollViewer>(ScrollerPart));

        ShowSeam(pinnedScrolled);
        UpdateCounts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BeginEditGesturesProperty)
        {
            body.Gestures = change.GetNewValue<BeginEditGestures>();
        }
        else if (change.Property == EditUnitProperty)
        {
            body.CommitRow();
            body.Unit = change.GetNewValue<GridEditUnit>();
        }
        else if (change.Property == CellActionsProperty)
        {
            body.Actions = change.GetNewValue<CellActions>();
            body.Refresh();
        }
        else if (change.Property == ColumnGesturesProperty)
        {
            columns.Gestures = change.GetNewValue<ColumnGestures>();
            frame.Rebuild();
        }
        else if (change.Property == SelectionUnitProperty)
        {
            body.Selection = change.GetNewValue<GridSelectionUnit>();
            body.Refresh();
        }
        else if (change.Property == IsLoadingProperty)
        {
            ShowEmpty();
        }
        else if (change.Property == SelectionActionsProperty)
        {
            UpdateCounts();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            // A grid draws GridRows. Anything else misses the cast in Follow and fills
            // every cell with nothing, so a plain list becomes a grid of blank rows with
            // no error anywhere. SetCurrentValue rather than an assignment, so wrapping
            // does not overwrite a binding the view wrote.
            if (change.GetNewValue<System.Collections.IEnumerable?>() is { } given and not GridRows)
            {
                SetCurrentValue(ItemsSourceProperty, new GridRows(given));
                return;
            }

            if (rows is not null)
            {
                rows.Rebuilding -= OnRowsRebuilding;
                rows.Rebuilt -= OnRowsRebuilt;
            }

            rows = ItemsSource as GridRows;

            if (rows is not null)
            {
                rows.Rebuilding += OnRowsRebuilding;
                rows.Rebuilt += OnRowsRebuilt;
            }

            CancelEdit();
            UpdateCounts();
        }
    }

    private void Follow(Control container, GridRow? row)
    {
        // Before the container is told anything, since it is about to stand for another
        // row. An editor left open would be showing one row's field over another row's
        // data, and the grid would still be holding a cell that has moved on.
        if (body.Holds(container))
        {
            body.CommitEdit();
        }

        switch (container)
        {
            case DataGridRow data:
                data.Attach(columns);
                ((IGridRowLayout)data).SetEditing(false);
                data.Follow(row);
                break;

            case GridGroupRow group:
                group.Follow(row);
                break;
        }

        // A recycled container carries the last row's mark, so the current cell is worked
        // out again rather than left where it was drawn.
        body.Refresh();
    }

    /// <summary>
    /// Puts the seam where the pinned columns end. The look is the theme's, and the only
    /// thing settled here is where the line falls, since a margin cannot be bound to a width.
    /// </summary>
    private void ShowSeam(bool scrolled)
    {
        SetAndRaise(PinnedWidthProperty, ref pinnedWidth, columns.PinnedWidth);
        SetAndRaise(IsPinnedScrolledProperty, ref pinnedScrolled, scrolled);

        PseudoClasses.Set(":pinned", pinnedWidth > 0);
        PseudoClasses.Set(":pinscrolled", pinnedWidth > 0 && scrolled);

        if (seam is not null)
        {
            seam.Margin = new Thickness(Math.Max(0, pinnedWidth - 1), 0, 0, 0);
        }
    }

    /// <summary>
    /// Which of the three empty states is on, if any. All three keep the toolbar and the
    /// header on screen, since a grid is a place rather than a page and the control that
    /// gets a person out of the state has to stay where it was.
    /// </summary>
    private void ShowEmpty()
    {
        var loading = IsLoading;

        // Emptied by a filter is not the same as never having had anything, and only one of
        // them teaches, so they are told apart by whether the source held anything at all.
        var filtered = !loading && rows is { Total: > 0, Matched: 0 };
        var nothing = !loading && !filtered && ItemCount == 0;

        PseudoClasses.Set(":loading", loading);
        PseudoClasses.Set(":nomatches", filtered);
        PseudoClasses.Set(":nothing", nothing);
    }

    private void OnSelectionDropped(object? sender, EventArgs e) => DropSelection();

    /// <summary>
    /// Unpicks every row and takes the block with it, since a bar that has gone leaves
    /// nothing behind that says what a following action would apply to.
    /// </summary>
    private void DropSelection()
    {
        UnselectAll();
        body.ClearBlock();
        Focus();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && body.BeginEditAt(source))
        {
            e.Handled = true;
        }
    }

    private void OnRowsRebuilding(object? sender, EventArgs e)
    {
        held = SelectedItems is { Count: > 0 } picked ? [.. picked.OfType<GridRow>()] : null;
        resting = frame.Offset;
    }

    private void OnRowsRebuilt(object? sender, EventArgs e)
    {
        // An edit whose row has left the list has nowhere to land. One still on the page
        // carries on, so an item arriving from the source does not end it.
        if (EditingCell?.FindAncestorOfType<DataGridRow>()?.Row is not { } open || rows?.Contains(open) != true)
        {
            CancelEdit();
        }

        body.Recount();
        Restore();

        // A page that turns starts at its first row. Staying where the last page was
        // scrolled to lands a person in the middle of a page they have not seen.
        if (rows is { } current && current.Page != page)
        {
            page = current.Page;
            frame.ToTop();
        }
        else
        {
            frame.Offset = resting;
        }

        UpdateCounts();
    }

    /// <summary>
    /// Picks again whatever was picked before the rebuild and is still in the list. A sort
    /// reorders every row it keeps, so it is drawn again from scratch, and the selection has
    /// to be put back by row rather than by where the row was.
    /// </summary>
    private void Restore()
    {
        var picked = held;

        held = null;

        if (picked is not { Count: > 0 } || SelectedItems is not { } current)
        {
            return;
        }

        var live = new HashSet<object>(ItemsView.Where(item => item is not null)!);
        var wanted = picked.Where(live.Contains).ToList();

        if (wanted.Count == current.Count && wanted.All(current.Contains))
        {
            return;
        }

        current.Clear();

        foreach (var row in wanted)
        {
            current.Add(row);
        }
    }

    private bool IsHeading(int index) =>
        index >= 0 && index < ItemsView.Count && ItemsView[index] is GridRow { IsGroup: true };

    /// <summary>
    /// Takes any heading back out of the selection. Select all goes straight to the
    /// selection model rather than through the arrow keys, so this is the one place that
    /// catches every way a heading can end up picked.
    /// </summary>
    private void DropHeadings()
    {
        if (dropping || SelectedItems is not { Count: > 0 } picked)
        {
            return;
        }

        dropping = true;

        try
        {
            for (var index = picked.Count - 1; index >= 0; index--)
            {
                if (picked[index] is GridRow { IsGroup: true })
                {
                    picked.RemoveAt(index);
                }
            }
        }
        finally
        {
            dropping = false;
        }
    }

    /// <summary>
    /// The sort spelled out. Past three keys it stops naming them and says how many more,
    /// since a footer that runs out of room says nothing at all.
    /// </summary>
    private string SortWords()
    {
        if (rows?.Sorts is not { Count: > 0 } sorts)
        {
            return string.Empty;
        }

        var named = sorts.Take(3).Select(term => term.Column.Header?.ToString() ?? string.Empty);
        var more = sorts.Count - 3;

        return more > 0
            ? $"sorted by {string.Join(", ", named)} and {more:N0} more"
            : $"sorted by {string.Join(", ", named)}";
    }

    private void UpdateCounts()
    {
        var picked = SelectedItems?.Count ?? 0;
        var owned = picked > 0 && SelectionActions is not null;

        PseudoClasses.Set(":picking", owned);

        SetAndRaise(PickedTextProperty, ref pickedText, owned ? $"{picked:N0} selected" : string.Empty);

        // A live block says what it holds instead of how many rows are picked, since the
        // same number twice over says nothing and the block is what a person is looking at.
        // The bar takes the count the same way, giving the footer back to the query.
        SetAndRaise(
            SelectionTextProperty,
            ref selectionText,
            body.RangeText is { Length: > 0 } range ? range
                : picked == 0 || owned ? string.Empty
                : $"{picked:N0} selected");

        SetAndRaise(
            ShownTextProperty,
            ref shownText,
            rows is null ? string.Empty : $"{rows.Shown:N0} of {rows.Total:N0} shown");

        SetAndRaise(SortTextProperty, ref sortText, SortWords());

        // A cell nobody has scrolled to still blocks a save, so this counts the whole source
        // rather than what is on screen.
        SetAndRaise(
            ErrorTextProperty,
            ref errorText,
            body.ErrorCount == 0 ? string.Empty
                : body.ErrorCount == 1 ? "1 cell needs fixing"
                : $"{body.ErrorCount:N0} cells need fixing");

        PseudoClasses.Set(":errors", body.ErrorCount > 0);

        ShowEmpty();
    }
}
