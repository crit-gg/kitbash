using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A grid's rows: the source sorted, grouped, paged and flattened to what can be seen.
/// It is what a <see cref="DataGrid"/> is given, the way <see cref="TreeRows"/> is what a
/// <see cref="Tree"/> is given.
/// </summary>
public sealed class GridRows : IReadOnlyList<GridRow>, IList, INotifyCollectionChanged
{
    /// <summary>Stands in for a group key of null, since a dictionary cannot hold one.</summary>
    private static readonly object NoKey = new();

    private readonly AvaloniaList<GridRow> rows = new();
    private readonly HashSet<object> collapsed = [];

    private IEnumerable source;
    private int pageSize;
    private int page = 1;

    public GridRows(IEnumerable source)
    {
        this.source = source;

        rows.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
        Build();
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Raised after a rebuild, so a grid can redraw counts that are not rows.</summary>
    public event EventHandler? Rebuilt;

    public int Count => rows.Count;

    public GridRow this[int index] => rows[index];

    /// <summary>The column the sort is on, or null when the source order stands.</summary>
    public GridColumn? SortColumn { get; private set; }

    public GridSortDirection SortDirection { get; private set; }

    /// <summary>What rows are grouped by, or null for no grouping.</summary>
    public Func<object, object?>? GroupKey { get; private set; }

    /// <summary>How many data rows a page holds. Zero is one continuous body, which is the default.</summary>
    public int PageSize
    {
        get => pageSize;
        set
        {
            pageSize = Math.Max(0, value);
            Build();
        }
    }

    /// <summary>Which page is shown, counting from one.</summary>
    public int Page
    {
        get => page;
        set
        {
            page = Math.Clamp(value, 1, PageCount);
            Build();
        }
    }

    /// <summary>How many pages there are. One when paging is off.</summary>
    public int PageCount => pageSize <= 0 ? 1 : Math.Max(1, (Visible + pageSize - 1) / pageSize);

    /// <summary>How many items the source holds.</summary>
    public int Total { get; private set; }

    /// <summary>How many data rows are in the list now.</summary>
    public int Shown { get; private set; }

    /// <summary>Where the page starts, counting from one, or zero when there is nothing.</summary>
    public int First { get; private set; }

    /// <summary>Where the page ends, counting from one.</summary>
    public int Last { get; private set; }

    /// <summary>How many data rows a page could be cut from, which leaves out a closed group.</summary>
    private int Visible { get; set; }

    /// <summary>Builds the whole thing again from a new source.</summary>
    public void Reset(IEnumerable source)
    {
        this.source = source;
        collapsed.Clear();
        page = 1;
        Build();
    }

    /// <summary>Builds the whole thing again from the source it already has.</summary>
    public void Refresh() => Build();

    /// <summary>
    /// Sorts on a column. A column with no <see cref="GridColumn.SortKey"/> cannot be
    /// sorted, so asking for one puts the source order back.
    /// </summary>
    public void Sort(GridColumn? column, GridSortDirection direction)
    {
        if (column?.CanSort != true || direction == GridSortDirection.None)
        {
            column = null;
            direction = GridSortDirection.None;
        }

        if (SortColumn is { } old && !ReferenceEquals(old, column))
        {
            old.SortDirection = GridSortDirection.None;
        }

        SortColumn = column;
        SortDirection = direction;

        if (column is not null)
        {
            column.SortDirection = direction;
        }

        Build();
    }

    /// <summary>Groups rows by a key, or drops the grouping when handed null.</summary>
    public void Group(Func<object, object?>? key)
    {
        GroupKey = key;
        collapsed.Clear();
        Build();
    }

    /// <summary>Opens a closed group, closes an open one.</summary>
    public void Toggle(GridRow row)
    {
        if (!row.IsGroup)
        {
            return;
        }

        var key = row.Key ?? NoKey;

        if (!collapsed.Remove(key))
        {
            collapsed.Add(key);
        }

        Build();
    }

    public IEnumerator<GridRow> GetEnumerator() => rows.GetEnumerator();

    private void Build()
    {
        var items = source.Cast<object?>().Where(item => item is not null).Select(item => item!).ToList();

        Total = items.Count;

        if (SortColumn?.SortKey is { } key && SortDirection != GridSortDirection.None)
        {
            var comparer = Comparer<object?>.Create(Compare);

            items = SortDirection == GridSortDirection.Ascending
                ? items.OrderBy(key, comparer).ToList()
                : items.OrderByDescending(key, comparer).ToList();
        }

        var built = GroupKey is null ? Flat(items) : Grouped(items);

        Visible = built.Count(row => !row.IsGroup);
        page = Math.Clamp(page, 1, PageCount);

        var paged = Slice(built);

        Shown = paged.Count(row => !row.IsGroup);

        var index = 0;

        foreach (var row in paged)
        {
            if (!row.IsGroup)
            {
                row.IsAlternate = index++ % 2 == 1;
            }
        }

        rows.Clear();
        rows.AddRange(paged);
        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    private static List<GridRow> Flat(List<object> items) => items.Select(item => new GridRow(item)).ToList();

    /// <summary>
    /// One header per key, in the order the keys first appear, with the group's rows under
    /// it unless it has been closed.
    /// </summary>
    private List<GridRow> Grouped(List<object> items)
    {
        var order = new List<object>();
        var groups = new Dictionary<object, List<object>>();

        foreach (var item in items)
        {
            var key = GroupKey!(item) ?? NoKey;

            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = group = [];
                order.Add(key);
            }

            group.Add(item);
        }

        var built = new List<GridRow>();

        foreach (var key in order)
        {
            var group = groups[key];
            var header = new GridRow(ReferenceEquals(key, NoKey) ? null : key, group.Count)
            {
                IsExpanded = !collapsed.Contains(key),
            };

            built.Add(header);

            if (header.IsExpanded)
            {
                built.AddRange(group.Select(item => new GridRow(item)));
            }
        }

        return built;
    }

    /// <summary>
    /// Cuts a page out, counting data rows alone. A group header comes along when any of
    /// its rows landed on the page, so a page never opens with rows under no heading.
    /// </summary>
    private List<GridRow> Slice(List<GridRow> built)
    {
        if (pageSize <= 0)
        {
            First = built.Count(row => !row.IsGroup) == 0 ? 0 : 1;
            Last = built.Count(row => !row.IsGroup);
            return built;
        }

        var skip = (page - 1) * pageSize;
        var paged = new List<GridRow>();
        var seen = 0;
        var kept = 0;
        GridRow? pending = null;

        foreach (var row in built)
        {
            if (row.IsGroup)
            {
                pending = row;
                continue;
            }

            if (seen++ < skip || kept == pageSize)
            {
                continue;
            }

            if (pending is not null)
            {
                paged.Add(pending);
                pending = null;
            }

            paged.Add(row);
            kept++;
        }

        First = kept == 0 ? 0 : skip + 1;
        Last = skip + kept;

        return paged;
    }

    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCulture);
    }

    // The read only list a virtualising panel needs. Indexing has to be direct rather than
    // by walking, since a panel asks for the rows around a scroll position.

    IEnumerator IEnumerable.GetEnumerator() => rows.GetEnumerator();

    bool IList.IsFixedSize => false;

    bool IList.IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => rows;

    object? IList.this[int index]
    {
        get => rows[index];
        set => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");
    }

    bool IList.Contains(object? value) => value is GridRow row && rows.Contains(row);

    int IList.IndexOf(object? value) => value is GridRow row ? rows.IndexOf(row) : -1;

    void ICollection.CopyTo(Array array, int index) => ((ICollection)rows).CopyTo(array, index);

    int IList.Add(object? value) => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");

    void IList.Clear() => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");

    void IList.Insert(int index, object? value) => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");

    void IList.Remove(object? value) => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");

    void IList.RemoveAt(int index) => throw new NotSupportedException("A grid's rows change by sorting, grouping and paging.");
}
