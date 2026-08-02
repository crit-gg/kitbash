using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace Workbench.Ui.Controls;

/// <summary>
/// A tree flattened to the rows that can be seen, so a tree can be virtualised.
/// </summary>
/// <remarks>
/// Avalonia has no virtualising tree and no virtualising tree panel. <c>ListBox</c>
/// overrides its panel to a virtualising one and gets that free, while <c>TreeView</c>
/// inherits the plain stack panel and so does every node under it, which means a tree of
/// ten thousand rows realises ten thousand controls.
/// <para>
/// So the standard answer: flatten the expanded nodes into a flat list, virtualise that,
/// and let expanding and collapsing edit the list rather than the tree of controls.
/// Depth becomes a value on a row, which is also what makes the indent and the guides a
/// calculation rather than a nesting.
/// </para>
/// <para>
/// Collapsing forgets. A subtree is dropped when it closes and built again when it opens,
/// so the list only ever holds rows a person could see.
/// </para>
/// <para>
/// This is a list of rows rather than a wrapper holding one, because that is what an
/// <c>ItemsSource</c> has to be. It reads as a list and refuses to be written as one:
/// rows arrive and leave through <see cref="Expand"/>, <see cref="Collapse"/> and
/// <see cref="Reset"/>.
/// </para>
/// </remarks>
public sealed class TreeRows : IReadOnlyList<TreeRow>, IList, INotifyCollectionChanged
{
    private readonly AvaloniaList<TreeRow> rows = new();
    private readonly Func<object, IEnumerable?> children;

    private IEnumerable roots;

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
        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            yield return new TreeRow(item, level, Any(children(item)));
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
