using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Utilities;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A grid's rows: the source filtered, sorted, grouped, paged and flattened to what can be
/// seen. It is what a <see cref="DataGrid"/> is given, the way <see cref="TreeRows"/> is
/// what a <see cref="Tree"/> is given.
/// </summary>
public sealed class GridRows
    : IReadOnlyList<GridRow>, IList, INotifyCollectionChanged, IWeakEventSubscriber<NotifyCollectionChangedEventArgs>
{
    /// <summary>Stands in for a group key of null, since a dictionary cannot hold one.</summary>
    private static readonly object NoKey = new();

    private readonly AvaloniaList<GridRow> rows = new();
    private readonly GridValueComparer order = new();
    private readonly List<GridSortTerm> sorts = [];

    /// <summary>One row per item, so a row picked before a rebuild is the same object after it.</summary>
    private Dictionary<object, GridRow> wrappers = new(ReferenceEqualityComparer.Instance);

    private Dictionary<object, GridRow> headers;
    private HashSet<object> collapsed;
    private IEqualityComparer<object> keys;

    private IEnumerable source;
    private Func<object, bool>? filter;
    private int pageSize;
    private int page = 1;

    public GridRows(IEnumerable source)
    {
        this.source = source;

        keys = new GroupKeys(EqualityComparer<object>.Default);
        headers = new Dictionary<object, GridRow>(keys);
        collapsed = new HashSet<object>(keys);

        rows.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);

        Watch(null, source);
        Build();
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Raised before the list is changed, so a grid can note what it has to put back.</summary>
    public event EventHandler? Rebuilding;

    /// <summary>Raised after a rebuild, so a grid can redraw counts that are not rows.</summary>
    public event EventHandler? Rebuilt;

    public int Count => rows.Count;

    public GridRow this[int index] => rows[index];

    /// <summary>The columns the sort runs over, first one first. Empty for the source order.</summary>
    public IReadOnlyList<GridSortTerm> Sorts => sorts;

    /// <summary>The column the sort leads on, or null when the source order stands.</summary>
    public GridColumn? SortColumn => sorts.Count > 0 ? sorts[0].Column : null;

    public GridSortDirection SortDirection => sorts.Count > 0 ? sorts[0].Direction : GridSortDirection.None;

    /// <summary>What rows are grouped by, or null for no grouping.</summary>
    public Func<object, object?>? GroupKey { get; private set; }

    /// <summary>
    /// Which items are rows at all. Null keeps every one of them. It sits here rather than
    /// over the source so <see cref="Total"/> keeps saying how many there are.
    /// </summary>
    public Func<object, bool>? Filter
    {
        get => filter;
        set
        {
            filter = value;
            page = 1;
            Build();
        }
    }

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

    /// <summary>How many items the source holds, whether or not the filter kept them.</summary>
    public int Total { get; private set; }

    /// <summary>How many items the filter kept. The same as <see cref="Total"/> when there is none.</summary>
    public int Matched { get; private set; }

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
        Watch(this.source, source);

        this.source = source;
        wrappers.Clear();
        headers.Clear();
        collapsed.Clear();
        page = 1;
        Build();
    }

    /// <summary>Builds the whole thing again from the source it already has.</summary>
    public void Refresh() => Build();

    /// <summary>
    /// Sorts on one column, dropping any other the sort was on. A column with no
    /// <see cref="GridColumn.SortKey"/> cannot be sorted, so asking for one puts the source
    /// order back.
    /// </summary>
    public void Sort(GridColumn? column, GridSortDirection direction) =>
        Resort(column?.CanSort == true && direction != GridSortDirection.None
            ? [new GridSortTerm(column, direction)]
            : []);

    /// <summary>
    /// Adds a column under the ones already sorted on, or moves the one already there.
    /// <see cref="GridSortDirection.None"/> takes it back out.
    /// </summary>
    public void AddSort(GridColumn column, GridSortDirection direction)
    {
        if (!column.CanSort)
        {
            return;
        }

        var next = sorts.Where(term => !ReferenceEquals(term.Column, column)).ToList();

        if (direction != GridSortDirection.None)
        {
            var at = sorts.FindIndex(term => ReferenceEquals(term.Column, column));

            next.Insert(at < 0 ? next.Count : at, new GridSortTerm(column, direction));
        }

        Resort(next);
    }

    /// <summary>Groups rows by a key, or drops the grouping when handed null.</summary>
    /// <param name="key">What a row's group is. Null drops the grouping.</param>
    /// <param name="keys">
    /// How two keys are told apart. The default is the key's own equality, which is what a
    /// key built fresh on every pass needs replacing to stay collapsed.
    /// </param>
    public void Group(Func<object, object?>? key, IEqualityComparer<object>? keys = null)
    {
        GroupKey = key;

        this.keys = new GroupKeys(keys ?? EqualityComparer<object>.Default);
        headers = new Dictionary<object, GridRow>(this.keys);
        collapsed = new HashSet<object>(this.keys);

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

    void IWeakEventSubscriber<NotifyCollectionChangedEventArgs>.OnEvent(
        object? sender,
        WeakEvent ev,
        NotifyCollectionChangedEventArgs e) => Build();

    /// <summary>
    /// Watches a source that says when it changes, so nothing has to call
    /// <see cref="Refresh"/>. The subscription is weak, since a source usually outlives the
    /// grid drawing it.
    /// </summary>
    private void Watch(IEnumerable? before, IEnumerable? after)
    {
        if (before is INotifyCollectionChanged old)
        {
            WeakEvents.CollectionChanged.Unsubscribe(old, this);
        }

        if (after is INotifyCollectionChanged fresh)
        {
            WeakEvents.CollectionChanged.Subscribe(fresh, this);
        }
    }

    private void Resort(List<GridSortTerm> next)
    {
        foreach (var term in sorts)
        {
            if (!next.Any(kept => ReferenceEquals(kept.Column, term.Column)))
            {
                term.Column.SortDirection = GridSortDirection.None;
                term.Column.SortOrder = 0;
            }
        }

        sorts.Clear();
        sorts.AddRange(next);

        for (var index = 0; index < sorts.Count; index++)
        {
            sorts[index].Column.SortDirection = sorts[index].Direction;
            sorts[index].Column.SortOrder = sorts.Count > 1 ? index + 1 : 0;
        }

        Build();
    }

    private void Build()
    {
        var items = new List<object>();

        foreach (var item in source)
        {
            if (item is not null)
            {
                items.Add(item);
            }
        }

        Total = items.Count;

        // Built fresh from the source each pass, carrying over the row an item already had,
        // so a row for an item that has gone does not stay in the map forever.
        var live = new Dictionary<object, GridRow>(ReferenceEqualityComparer.Instance);

        foreach (var item in items)
        {
            if (!live.ContainsKey(item))
            {
                live[item] = wrappers.TryGetValue(item, out var kept) ? kept : new GridRow(item);
            }
        }

        wrappers = live;

        if (filter is { } keep)
        {
            items = items.Where(keep).ToList();
        }

        Matched = items.Count;

        if (sorts.Count > 0)
        {
            items = [.. items.OrderBy(item => item, new GridSortComparer([.. sorts], order))];
        }

        var used = new HashSet<GridRow>();
        var built = GroupKey is null ? Flat(items, used) : Grouped(items, used);

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

        Rebuilding?.Invoke(this, EventArgs.Empty);
        Splice(paged);
        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The row standing for an item. The same item twice over needs a row each, and only
    /// the first of them is the one the map hands back.
    /// </summary>
    private GridRow Wrap(object item, HashSet<GridRow> used) =>
        wrappers.TryGetValue(item, out var row) && used.Add(row) ? row : new GridRow(item);

    private List<GridRow> Flat(List<object> items, HashSet<GridRow> used) =>
        [.. items.Select(item => Wrap(item, used))];

    /// <summary>
    /// One header per key, in the order the keys first appear, with the group's rows under
    /// it unless it has been closed.
    /// </summary>
    private List<GridRow> Grouped(List<object> items, HashSet<GridRow> used)
    {
        var seen = new List<object>();
        var groups = new Dictionary<object, List<object>>(keys);

        foreach (var item in items)
        {
            var key = GroupKey!(item) ?? NoKey;

            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = group = [];
                seen.Add(key);
            }

            group.Add(item);
        }

        var live = new Dictionary<object, GridRow>(keys);
        var built = new List<GridRow>();

        foreach (var key in seen)
        {
            var group = groups[key];

            var header = headers.TryGetValue(key, out var kept)
                ? kept
                : new GridRow(ReferenceEquals(key, NoKey) ? null : key, group.Count);

            header.Count = group.Count;
            header.IsExpanded = !collapsed.Contains(key);

            live[key] = header;
            built.Add(header);

            if (header.IsExpanded)
            {
                built.AddRange(group.Select(item => Wrap(item, used)));
            }
        }

        headers = live;

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

    /// <summary>
    /// Moves the list to what was built, taking rows out and putting rows in rather than
    /// replacing the lot, so a container, a selection and a scroll position all survive
    /// anything that leaves the rows it keeps in the order they were already in.
    /// </summary>
    private void Splice(List<GridRow> next)
    {
        var wanted = new HashSet<GridRow>(next);
        var have = new HashSet<GridRow>(rows);

        // A sort reorders the rows it keeps, and no run of inserts and removes describes
        // that, so it is the one case that goes through a reset.
        if (!Ordered(next, wanted, have))
        {
            rows.Clear();
            rows.AddRange(next);
            return;
        }

        var at = rows.Count - 1;

        while (at >= 0)
        {
            if (wanted.Contains(rows[at]))
            {
                at--;
                continue;
            }

            var last = at;

            while (at >= 0 && !wanted.Contains(rows[at]))
            {
                at--;
            }

            rows.RemoveRange(at + 1, last - at);
        }

        at = 0;

        while (at < next.Count)
        {
            if (at < rows.Count && ReferenceEquals(rows[at], next[at]))
            {
                at++;
                continue;
            }

            // Everything up to the row already sitting here, which is the next survivor.
            var survivor = at < rows.Count ? rows[at] : null;
            var end = at;

            while (end < next.Count && !ReferenceEquals(next[end], survivor))
            {
                end++;
            }

            rows.InsertRange(at, next.GetRange(at, end - at));
            at = survivor is null ? end : end + 1;
        }
    }

    /// <summary>Whether the rows in both lists are in the same order in each.</summary>
    private bool Ordered(List<GridRow> next, HashSet<GridRow> wanted, HashSet<GridRow> have)
    {
        var mine = 0;
        var theirs = 0;

        while (true)
        {
            while (mine < rows.Count && !wanted.Contains(rows[mine]))
            {
                mine++;
            }

            while (theirs < next.Count && !have.Contains(next[theirs]))
            {
                theirs++;
            }

            if (mine >= rows.Count || theirs >= next.Count)
            {
                return mine >= rows.Count && theirs >= next.Count;
            }

            if (!ReferenceEquals(rows[mine], next[theirs]))
            {
                return false;
            }

            mine++;
            theirs++;
        }
    }

    /// <summary>
    /// The caller's key equality, with the stand in for a null key kept away from it, since
    /// a comparer written for real keys has no reason to expect one.
    /// </summary>
    private sealed class GroupKeys(IEqualityComparer<object> inner) : IEqualityComparer<object>
    {
        public new bool Equals(object? left, object? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || ReferenceEquals(left, NoKey) || ReferenceEquals(right, NoKey))
            {
                return false;
            }

            return inner.Equals(left, right);
        }

        public int GetHashCode(object value) => ReferenceEquals(value, NoKey) ? 0 : inner.GetHashCode(value);
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
