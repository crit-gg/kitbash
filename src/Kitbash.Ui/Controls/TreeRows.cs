using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A tree flattened to the rows that can be seen, so a tree can be virtualised.
/// </summary>
public sealed class TreeRows : IReadOnlyList<TreeRow>, IList, INotifyCollectionChanged
{
    private readonly AvaloniaList<TreeRow> rows = new();
    private readonly Func<object, IEnumerable?> children;

    private IEnumerable roots;
    private IComparer<object>? order;
    private Func<object, bool>? match;

    /// <param name="roots">The items at the top of the tree.</param>
    /// <param name="children">
    /// What sits under an item. Null or empty makes it a leaf, and it is asked again every
    /// time the item is expanded rather than being held on to.
    /// </param>
    public TreeRows(IEnumerable roots, Func<object, IEnumerable?> children)
    {
        this.roots = roots;
        this.children = children;

        rows.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
        rows.AddRange(Build(roots, 0));
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => rows.Count;

    public TreeRow this[int index] => rows[index];

    /// <summary>Builds the whole thing again from a new set of roots.</summary>
    public void Reset(IEnumerable roots)
    {
        this.roots = roots;
        rows.Clear();
        rows.AddRange(Build(roots, 0));
    }

    /// <summary>Builds the whole thing again from the roots it already has.</summary>
    public void Reset() => Reset(roots);

    /// <summary>
    /// Orders siblings under every parent, or drops the ordering when handed null. It
    /// holds for a branch opened later too, since expanding builds its rows the same way.
    /// The tree is rebuilt, so everything closes.
    /// </summary>
    public void Sort(IComparer<object>? order)
    {
        this.order = order;
        Reset();
    }

    /// <summary>
    /// Which items are rows at all, or null for every one of them. A row survives when it
    /// matches or when anything under it does, so nothing is on screen that is not a match
    /// or the way to one. The tree is rebuilt, so everything closes.
    /// </summary>
    public void Filter(Func<object, bool>? match)
    {
        this.match = match;
        Reset();
    }

    /// <summary>Opens a row, putting its children in the list under it.</summary>
    public void Expand(TreeRow row)
    {
        var index = rows.IndexOf(row);

        if (index < 0 || row.IsExpanded || !row.HasChildren)
        {
            return;
        }

        var opened = Build(children(row.Item) ?? Array.Empty<object>(), row.Level + 1).ToList();

        // Asked again rather than trusted, since what a row holds can have changed since
        // the row was made.
        if (opened.Count == 0)
        {
            row.HasChildren = false;
            return;
        }

        row.IsExpanded = true;
        rows.InsertRange(index + 1, opened);
    }

    /// <summary>Closes a row, taking its whole subtree out of the list.</summary>
    public void Collapse(TreeRow row)
    {
        var index = rows.IndexOf(row);

        if (index < 0 || !row.IsExpanded)
        {
            return;
        }

        row.IsExpanded = false;

        // Everything below it that is deeper than it is, which is the subtree however
        // deep it goes, because a flat list keeps a subtree contiguous.
        var end = index + 1;

        while (end < rows.Count && rows[end].Level > row.Level)
        {
            end++;
        }

        rows.RemoveRange(index + 1, end - index - 1);
    }

    public void Toggle(TreeRow row)
    {
        if (row.IsExpanded)
        {
            Collapse(row);
        }
        else
        {
            Expand(row);
        }
    }

    /// <summary>Where a row sits, or minus one when it is not in the list.</summary>
    public int IndexOf(TreeRow row) => rows.IndexOf(row);

    /// <summary>The row this one hangs under, or null when it is a root.</summary>
    public TreeRow? ParentOf(TreeRow row)
    {
        var index = rows.IndexOf(row);

        for (var above = index - 1; above >= 0; above--)
        {
            if (rows[above].Level < row.Level)
            {
                return rows[above];
            }
        }

        return null;
    }

    public IEnumerator<TreeRow> GetEnumerator() => rows.GetEnumerator();

    private IEnumerable<TreeRow> Build(IEnumerable items, int level)
    {
        var kept = items.Cast<object?>().Where(item => item is not null).Select(item => item!);

        if (order is { } sorting)
        {
            kept = kept.OrderBy(item => item, sorting);
        }

        if (match is null)
        {
            foreach (var item in kept)
            {
                yield return new TreeRow(item, level, Any(children(item)));
            }

            yield break;
        }

        // A filter costs the whole tree, since whether a branch survives cannot be known
        // without walking everything under it. Without one only what is open is ever built.
        foreach (var item in kept)
        {
            var under = Build(children(item) ?? Array.Empty<object>(), level + 1).ToList();

            if (under.Count == 0 && !match(item))
            {
                continue;
            }

            // Open, since a branch kept for something under it hides that thing when closed.
            yield return new TreeRow(item, level, under.Count > 0) { IsExpanded = under.Count > 0 };

            foreach (var row in under)
            {
                yield return row;
            }
        }
    }

    private static bool Any(IEnumerable? items)
    {
        if (items is null)
        {
            return false;
        }

        if (items is ICollection collection)
        {
            return collection.Count > 0;
        }

        var enumerator = items.GetEnumerator();

        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    // The read only list a virtualising panel needs. Indexing has to be direct rather than
    // by walking, since a panel asks for the rows around a scroll position and a walk
    // would make that cost the whole list.

    IEnumerator IEnumerable.GetEnumerator() => rows.GetEnumerator();

    bool IList.IsFixedSize => false;

    bool IList.IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => rows;

    object? IList.this[int index]
    {
        get => rows[index];
        set => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");
    }

    bool IList.Contains(object? value) => value is TreeRow row && rows.Contains(row);

    int IList.IndexOf(object? value) => value is TreeRow row ? IndexOf(row) : -1;

    void ICollection.CopyTo(Array array, int index) => ((ICollection)rows).CopyTo(array, index);

    int IList.Add(object? value) => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");

    void IList.Clear() => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");

    void IList.Insert(int index, object? value) => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");

    void IList.Remove(object? value) => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");

    void IList.RemoveAt(int index) => throw new NotSupportedException("A tree's rows change by expanding and collapsing.");
}
