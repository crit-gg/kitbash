namespace Kitbash.Ui.Controls;

/// <summary>
/// What is picked. Items and wires are held apart, since a wire has no box and the two are
/// never picked together by any gesture.
/// </summary>
public sealed class GraphSelection
{
    // A list rather than a set, because the order things were picked in is the order the
    // arrange operations read: everything lines up with the last one, which is the one a
    // person just clicked and the one they are looking at.
    private readonly List<GraphItem> _items = [];
    private readonly HashSet<GraphItem> _held = [];
    private readonly HashSet<GraphLink> _links = [];

    public event EventHandler? Changed;

    /// <summary>What is picked, in the order it was picked.</summary>
    public IReadOnlyList<GraphItem> Items => _items;

    /// <summary>The last thing picked, which is what an arrange lines everything up with.</summary>
    public GraphItem? Anchor => _items.Count > 0 ? _items[^1] : null;

    public IReadOnlyCollection<GraphLink> Links => _links;

    public IEnumerable<GraphNode> Nodes => _items.OfType<GraphNode>();

    public int Count => _items.Count + _links.Count;

    public bool Contains(GraphItem item) => _held.Contains(item);

    public bool Contains(GraphLink link) => _links.Contains(link);

    public void Clear()
    {
        if (_items.Count == 0 && _links.Count == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            item.IsSelected = false;
        }

        foreach (var link in _links)
        {
            link.IsSelected = false;
        }

        _items.Clear();
        _held.Clear();
        _links.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Set(GraphItem item)
    {
        Clear();
        Add(item);
    }

    public void Set(GraphLink link)
    {
        Clear();
        Add(link);
    }

    public void Add(GraphItem item)
    {
        if (!_held.Add(item))
        {
            return;
        }

        _items.Add(item);
        item.IsSelected = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Add(GraphLink link)
    {
        if (!_links.Add(link))
        {
            return;
        }

        link.IsSelected = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(GraphItem item)
    {
        if (!_held.Remove(item))
        {
            return;
        }

        _items.Remove(item);
        item.IsSelected = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(GraphItem item)
    {
        if (_held.Contains(item))
        {
            Remove(item);
        }
        else
        {
            Add(item);
        }
    }

    /// <summary>
    /// Replaces the picked items with a run, keeping whatever was held before the drag
    /// started. What a marquee writes on every move, so it does the least it can.
    /// </summary>
    public void SetRun(IReadOnlyList<GraphItem> found, IReadOnlyList<GraphItem> kept)
    {
        var wanted = new HashSet<GraphItem>(kept);

        foreach (var item in found)
        {
            wanted.Add(item);
        }

        if (wanted.SetEquals(_held))
        {
            return;
        }

        foreach (var item in _items)
        {
            item.IsSelected = false;
        }

        _items.Clear();
        _held.Clear();

        // What was held before the band comes first, so the anchor a person had picked by
        // hand is not lost behind whatever the band happened to sweep up.
        foreach (var item in kept)
        {
            if (_held.Add(item))
            {
                _items.Add(item);
            }
        }

        foreach (var item in found)
        {
            if (_held.Add(item))
            {
                _items.Add(item);
            }
        }

        foreach (var item in _items)
        {
            item.IsSelected = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
