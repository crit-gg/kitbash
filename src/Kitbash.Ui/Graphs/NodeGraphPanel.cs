using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The realised part of the graph. It holds a container for what meets the viewport and
/// nothing else, recycling the rest into a pool, which is the same answer the tree and both
/// grids give in one dimension applied to two.
///
/// At <see cref="GraphLod.Block"/> it realises nothing at all and paints the boxes itself,
/// since a name at that size is a smudge that costs more than everything else together.
/// </summary>
public sealed class NodeGraphPanel : Control
{
    // How far outside the viewport an item is still realised, in graph units. Wide enough
    // that a node is ready before its edge appears and narrow enough to stay bounded.
    private const double Reach = 160;

    // How many spare containers of a kind are kept hidden in the tree.
    private const int Spare = 96;

    private readonly Dictionary<GraphItem, Control> _live = [];
    private readonly Stack<NodeCard> _nodes = [];
    private readonly Stack<GraphFrameCard> _frames = [];
    private readonly Stack<GraphNoteCard> _notes = [];

    private readonly List<GraphItem> _found = [];
    private readonly HashSet<GraphItem> _wanted = [];
    private readonly List<GraphItem> _going = [];

    private readonly MatrixTransform _look = new();

    public NodeGraphPanel()
    {
        ClipToBounds = false;
        RenderTransform = _look;
        RenderTransformOrigin = RelativePoint.TopLeft;
    }

    internal NodeGraph? Graph { get; set; }

    /// <summary>How many containers are realised. What a test reads to hold the rule.</summary>
    public int RealisedCount => _live.Count;

    /// <summary>
    /// Works out what should exist and makes it so. Called on every view change and on every
    /// change to the set of items, and it does nothing at all when neither moved anything in
    /// or out of view.
    /// </summary>
    public void Sync()
    {
        if (Graph is not { Model: { } model } graph)
        {
            Drop();
            return;
        }

        var view = graph.View;

        // The pan and the zoom ride on the panel's own transform, so moving the view
        // arranges nothing and redraws no card. A card is arranged when it is realised and
        // when its node moves, and at no other time.
        _look.Matrix = view.Matrix;

        if (view.Lod == GraphLod.Block)
        {
            Drop();
            InvalidateVisual();
            return;
        }

        _found.Clear();
        _wanted.Clear();
        _going.Clear();

        model.Query(view.Viewport(Bounds.Size).Inflate(Reach), _found);

        foreach (var item in _found)
        {
            _wanted.Add(item);
        }

        foreach (var (item, _) in _live)
        {
            if (!_wanted.Contains(item))
            {
                _going.Add(item);
            }
        }

        foreach (var item in _going)
        {
            Recycle(item);
        }

        foreach (var item in _found)
        {
            if (!_live.ContainsKey(item))
            {
                Realise(graph, item);
            }
        }

        InvalidateArrange();
    }

    /// <summary>
    /// Tells every realised card about its node again. A node that collapses or grows a body
    /// while it is on screen changes which slots it should have, and it is already realised,
    /// so nothing else would ask. Only a change to the model runs it, never a pan.
    /// </summary>
    public void Reslot()
    {
        if (Graph is not { } graph)
        {
            return;
        }

        foreach (var (item, container) in _live)
        {
            if (item is GraphNode node && container is NodeCard card)
            {
                card.Follow(graph, node);
            }
        }
    }

    /// <summary>Lets every container go, which a new model or a theme change asks for.</summary>
    public void Drop()
    {
        foreach (var item in _live.Keys.ToArray())
        {
            Recycle(item);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { Model: { } model } graph || graph.View.Lod != GraphLod.Block)
        {
            return;
        }

        // Already in graph units, since the view rides on this panel's own transform.
        _found.Clear();
        model.Query(graph.View.Viewport(Bounds.Size), _found);

        var colours = graph.Colours;
        var radius = graph.Metrics.NodeRadius;

        // A frame is the most useful thing at this size, since it is the only part of a
        // graph still readable when a node's name is not, so it is drawn before them.
        foreach (var item in _found)
        {
            if (item is GraphFrame frame)
            {
                context.DrawRectangle(
                    colours.FrameWash,
                    graph.FramePen(frame.IsSelected ? graph.AccentColour : frame.Colour),
                    new RoundedRect(frame.Bounds, radius));
            }
            else if (item is GraphNote note)
            {
                context.DrawRectangle(colours.Header, null, new RoundedRect(note.Bounds, radius));
            }
        }

        foreach (var item in _found)
        {
            if (item is not GraphNode node)
            {
                continue;
            }

            var ink = node.IsSelected
                ? colours.Accent
                : node.IsBypassed
                    ? colours.NodeOff
                    : graph.Kinds[node.Kind].Ink;

            context.DrawRectangle(ink, null, new RoundedRect(node.Bounds, radius));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The viewport is read off this panel, so what is realised cannot be worked out
        // until it has one. The first pass runs here rather than at template time.
        if (change.Property == BoundsProperty)
        {
            Sync();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var (item, container) in _live)
        {
            container.Measure(Size(item));
        }

        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (item, container) in _live)
        {
            var lead = container is GraphFrameCard frame ? frame.Lead : 0;

            container.Arrange(new Rect(new Point(item.X, item.Y - lead), Size(item)));
        }

        return finalSize;
    }

    private Size Size(GraphItem item)
    {
        var lead = item is GraphFrame ? (Graph?.Metrics.FrameLabelHeight ?? 20) + 3 : 0;

        return new Size(item.Width, item.Height + lead);
    }

    private void Realise(NodeGraph graph, GraphItem item)
    {
        Control container;

        switch (item)
        {
            case GraphNode node:
            {
                var card = _nodes.Count > 0 ? _nodes.Pop() : New<NodeCard>();

                card.Follow(graph, node);
                container = card;
                break;
            }

            case GraphFrame frame:
            {
                var card = _frames.Count > 0 ? _frames.Pop() : New<GraphFrameCard>();

                card.Follow(graph, frame);
                container = card;
                break;
            }

            case GraphNote note:
            {
                var card = _notes.Count > 0 ? _notes.Pop() : New<GraphNoteCard>();

                card.Follow(graph, note);
                container = card;
                break;
            }

            default:
                return;
        }

        _live[item] = container;
        container.IsVisible = true;
        container.Measure(Size(item));
    }

    /// <summary>Every realised container, which the graph repaints through.</summary>
    internal IEnumerable<Control> Live => _live.Values;

    /// <summary>The container holding one item, or none when it is not realised.</summary>
    internal Control? Container(GraphItem item) => _live.GetValueOrDefault(item);

    /// <summary>Draws one item's container again, including anything layered over it.</summary>
    internal void Redraw(GraphItem item)
    {
        switch (_live.GetValueOrDefault(item))
        {
            case NodeCard card:
                card.Redraw();
                break;

            case { } container:
                container.InvalidateVisual();
                break;
        }
    }

    /// <summary>Draws every realised container again.</summary>
    internal void RedrawAll()
    {
        foreach (var container in _live.Values)
        {
            if (container is NodeCard card)
            {
                card.Redraw();
            }
            else
            {
                container.InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Keeps a container for the next item, up to a ceiling. Past it the container really is
    /// let go, so a window that was briefly enormous does not hold the tree it needed then.
    /// </summary>
    private void Keep<T>(Stack<T> pool, T container)
        where T : Control
    {
        if (pool.Count >= Spare)
        {
            LogicalChildren.Remove(container);
            VisualChildren.Remove(container);
            return;
        }

        pool.Push(container);
    }

    private T New<T>()
        where T : Control, new()
    {
        var container = new T();

        LogicalChildren.Add(container);
        VisualChildren.Add(container);

        return container;
    }

    private void Recycle(GraphItem item)
    {
        if (!_live.Remove(item, out var container))
        {
            return;
        }

        // A pooled container stays in the tree, hidden. Taking it out and putting it back
        // runs the whole attach and restyle path, which is what a pool exists to avoid, and
        // layout and drawing both skip anything invisible anyway.
        container.IsVisible = false;

        switch (container)
        {
            case NodeCard card:
                card.Release();
                Keep(_nodes, card);
                break;

            case GraphFrameCard card:
                card.Release();
                Keep(_frames, card);
                break;

            case GraphNoteCard card:
                card.Release();
                Keep(_notes, card);
                break;
        }
    }
}
