using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The graph itself: the nodes, the wires, the frames and the notes, with the spatial index
/// under them. Nothing here draws, and nothing here evaluates.
/// </summary>
public sealed class GraphModel
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphLink> _links = [];
    private readonly List<GraphFrame> _frames = [];
    private readonly List<GraphNote> _notes = [];

    private readonly Dictionary<string, GraphItem> _byId = [];
    private readonly Dictionary<GraphNode, List<GraphLink>> _touching = [];
    private readonly Dictionary<GraphPort, GraphLink> _intoPort = [];

    private readonly GraphIndex _index;
    private readonly LinkIndex _wires;
    private GraphMetrics _metrics = GraphMetrics.Dense;
    private Rect _content;
    private bool _contentKnown;

    public GraphModel()
    {
        _index = new GraphIndex(_metrics.IndexCell);
        _wires = new LinkIndex(_metrics.IndexCell);
    }

    /// <summary>Raised whenever anything changed, saying how much has to be done again.</summary>
    public event EventHandler<GraphChangedEventArgs>? Changed;

    /// <summary>The geometry every node and every wire is worked out from.</summary>
    public GraphMetrics Metrics
    {
        get => _metrics;
        set
        {
            if (ReferenceEquals(_metrics, value))
            {
                return;
            }

            _metrics = value;
            Rebuild();
        }
    }

    public IReadOnlyList<GraphNode> Nodes => _nodes;

    public IReadOnlyList<GraphLink> Links => _links;

    public IReadOnlyList<GraphFrame> Frames => _frames;

    public IReadOnlyList<GraphNote> Notes => _notes;

    /// <summary>The box holding everything, which is what Fit reads.</summary>
    public Rect ContentBounds
    {
        get
        {
            if (_contentKnown)
            {
                return _content;
            }

            var known = false;
            var box = default(Rect);

            foreach (var item in Items())
            {
                box = known ? box.Union(item.Bounds) : item.Bounds;
                known = true;
            }

            _content = known ? box : new Rect(0, 0, 400, 300);
            _contentKnown = true;

            return _content;
        }
    }

    public GraphItem? Find(string id) => _byId.GetValueOrDefault(id);

    public GraphNode Add(GraphNode node)
    {
        Attach(node, _nodes);
        return node;
    }

    public GraphFrame Add(GraphFrame frame)
    {
        Attach(frame, _frames);
        return frame;
    }

    public GraphNote Add(GraphNote note)
    {
        Attach(note, _notes);
        return note;
    }

    /// <summary>
    /// Joins two ports. An input holds one wire, so whatever was already in it is taken out
    /// first, which is what every node editor does and what a person expects.
    /// </summary>
    public GraphLink Add(GraphLink link)
    {
        if (_intoPort.TryGetValue(link.To, out var already))
        {
            Remove(already);
        }

        link.Owner = this;
        _links.Add(link);
        _intoPort[link.To] = link;

        link.Indexed = link.Box(_metrics);
        _wires.Add(link, link.Indexed);

        Touching(link.FromNode).Add(link);
        Touching(link.ToNode).Add(link);

        Raise(GraphChange.Set);
        return link;
    }

    /// <summary>Joins two ports when the rules allow it, and answers null when they do not.</summary>
    public GraphLink? TryJoin(GraphPort from, GraphPort to, IPortRules rules)
    {
        if (from.Direction == to.Direction || from.Node is null || to.Node is null)
        {
            return null;
        }

        var (output, input) = from.Direction == PortDirection.Output ? (from, to) : (to, from);

        if (ReferenceEquals(output.Node, input.Node) || !rules.CanJoin(output, input))
        {
            return null;
        }

        return Add(new GraphLink(output, input));
    }

    public void Remove(GraphLink link)
    {
        if (!_links.Remove(link))
        {
            return;
        }

        link.Owner = null;
        _wires.Remove(link, link.Indexed);
        _intoPort.Remove(link.To);
        Touching(link.FromNode).Remove(link);
        Touching(link.ToNode).Remove(link);

        Raise(GraphChange.Set);
    }

    /// <summary>Takes a node off with every wire that touched it.</summary>
    public void Remove(GraphNode node)
    {
        if (_touching.TryGetValue(node, out var wires))
        {
            foreach (var link in wires.ToArray())
            {
                Remove(link);
            }

            _touching.Remove(node);
        }

        Detach(node, _nodes);
    }

    public void Remove(GraphFrame frame) => Detach(frame, _frames);

    public void Remove(GraphNote note) => Detach(note, _notes);

    /// <summary>The wire in an input, or none.</summary>
    public GraphLink? LinkInto(GraphPort input) => _intoPort.GetValueOrDefault(input);

    /// <summary>Whether anything is wired to this port.</summary>
    public bool IsWired(GraphPort port)
    {
        if (port.Direction == PortDirection.Input)
        {
            return _intoPort.ContainsKey(port);
        }

        if (port.Node is null || !_touching.TryGetValue(port.Node, out var wires))
        {
            return false;
        }

        foreach (var link in wires)
        {
            if (ReferenceEquals(link.From, port))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every wire with an end on this node.</summary>
    public IReadOnlyList<GraphLink> LinksTouching(GraphNode node) =>
        _touching.TryGetValue(node, out var wires) ? wires : [];

    /// <summary>Everything with a box, in the order it is drawn.</summary>
    public IEnumerable<GraphItem> Items()
    {
        foreach (var frame in _frames)
        {
            yield return frame;
        }

        foreach (var note in _notes)
        {
            yield return note;
        }

        foreach (var node in _nodes)
        {
            yield return node;
        }
    }

    /// <summary>Everything whose box meets the area, appended to the list.</summary>
    public void Query(Rect area, List<GraphItem> into) => _index.Query(area, into);

    /// <summary>
    /// Every wire whose box meets the area, appended to the list. What a draw culls with and
    /// what a hit test reads, so neither costs the number of wires in the graph.
    /// </summary>
    public void QueryLinks(Rect area, List<GraphLink> into) => _wires.Query(area, into);

    /// <summary>Says the wires and the boxes are all stale, which a density change is.</summary>
    public void Rebuild()
    {
        // Emptied rather than replaced. Both indexes tell one query's results from the last
        // one's by a counter they hold, and a fresh index starts that counter over while
        // every item still holds a stamp from the old one, so the next query would skip
        // everything it had already returned.
        _index.Clear(_metrics.IndexCell);
        _wires.Clear(_metrics.IndexCell);

        foreach (var item in Items())
        {
            item.Refresh();
            _index.Add(item);
        }

        foreach (var link in _links)
        {
            link.Invalidate();
            link.Indexed = link.Box(_metrics);
            _wires.Add(link, link.Indexed);
        }

        Raise(GraphChange.Set);
    }

    /// <summary>Repaint, with nothing moved. What a link asks for when its route is stale.</summary>
    internal void Invalidate() => Raise(GraphChange.Look);

    /// <summary>
    /// Puts a wire back in the index under its new box. Only a node moving or a reroute being
    /// dragged calls it, so the work is bounded by what is being dragged.
    /// </summary>
    internal void LinkMoved(GraphLink link)
    {
        var now = link.Box(_metrics);

        _wires.Update(link, link.Indexed, now);
        link.Indexed = now;
    }

    internal void ItemChanged(GraphItem item, Rect was, GraphChange change)
    {
        if (change == GraphChange.Box)
        {
            _index.Update(item, was);
            _contentKnown = false;

            if (item is GraphNode node && _touching.TryGetValue(node, out var wires))
            {
                foreach (var link in wires)
                {
                    link.Invalidate();
                    LinkMoved(link);
                }
            }
        }

        Raise(change);
    }

    private List<GraphLink> Touching(GraphNode node)
    {
        if (!_touching.TryGetValue(node, out var wires))
        {
            _touching[node] = wires = [];
        }

        return wires;
    }

    private void Attach<T>(T item, List<T> list)
        where T : GraphItem
    {
        item.Owner = this;
        item.Refresh();
        list.Add(item);
        _byId[item.Id] = item;
        _index.Add(item);
        _contentKnown = false;

        Raise(GraphChange.Set);
    }

    private void Detach<T>(T item, List<T> list)
        where T : GraphItem
    {
        if (!list.Remove(item))
        {
            return;
        }

        _index.Remove(item, item.Bounds);
        _byId.Remove(item.Id);
        item.Owner = null;
        _contentKnown = false;

        Raise(GraphChange.Set);
    }

    private void Raise(GraphChange change) => Changed?.Invoke(this, new GraphChangedEventArgs(change));
}

/// <summary>What changed, so a listener can do the least it can get away with.</summary>
public sealed class GraphChangedEventArgs(GraphChange change) : EventArgs
{
    public GraphChange Change { get; } = change;
}
