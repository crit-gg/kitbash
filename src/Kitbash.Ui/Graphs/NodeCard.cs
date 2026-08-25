using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One node, drawn. The whole card is painted in a single pass rather than composed from
/// thirty controls, because a graph realises these by the dozen and text laid out once is
/// the difference between a graph that pans and one that stutters.
///
/// A tool that needs real controls inside a node sets <see cref="NodeGraph.NodeTemplate"/>,
/// and then the card draws the frame, the header and the pins and the template fills the
/// body. The pins stay the library's either way, so a wire always ends where one is drawn.
/// </summary>
public sealed class NodeCard : Control
{
    private const double IconBox = 24;
    private const double IconSize = 14;
    private const double CaretSize = 12;
    private const double MarkSize = 13;
    private const double HeaderLead = 10;
    private const double Gap = 7;
    private const double RowLead = 12;
    private const double ValuePad = 5;

    private ContentPresenter? _body;
    private ContentPresenter? _footer;
    private NodePins? _pins;

    /// <summary>The canvas this card belongs to.</summary>
    public NodeGraph? Graph { get; private set; }

    /// <summary>The node it is showing, or none while it is pooled.</summary>
    public GraphNode? Node { get; private set; }

    /// <summary>
    /// Everything a container is told is told here, and everything it is told is untold here
    /// too. A pooled card is handed a different node with nothing of the last one on it.
    /// </summary>
    internal void Follow(NodeGraph graph, GraphNode node)
    {
        Graph = graph;
        Node = node;
        ZIndex = node.Layer;

        // A drawn card holds nothing that could want a press, and the canvas hit tests the
        // model rather than the tree, so it takes itself out of the tree's hit testing
        // entirely. That is what keeps a pointer moving over a large graph from paying for
        // pointer over bookkeeping on every card it crosses.
        IsHitTestVisible = graph.NodeTemplate is not null;

        // A collapsed node is its header and nothing else, so it grows neither a body nor a
        // strip. Left in place the strip is laid out against the collapsed height and covers
        // the very header it collapsed to.
        var composed = graph.NodeTemplate is not null && !node.IsCollapsed;

        _body = Slot(_body, composed ? graph.NodeTemplate : null, node);
        _footer = Slot(_footer, composed && node.FooterHeight > 0 ? graph.FooterTemplate : null, node);

        // A visual child draws after Render, so a body would paint over the pins on the edge
        // it fills. With one in the way the pins move to a layer above it.
        _pins = composed ? Over() : Under();

        InvalidateVisual();
    }

    internal void Release()
    {
        Graph = null;
        Node = null;

        if (_body is not null)
        {
            _body.Content = null;
        }

        if (_footer is not null)
        {
            _footer.Content = null;
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new NodeCardAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph || Node is not { } node)
        {
            return;
        }

        var metrics = graph.Metrics;
        var colours = graph.Colours;
        var kind = graph.Kinds[node.Kind];
        var off = node.IsBypassed;
        var radius = metrics.NodeRadius;
        var box = new Rect(Bounds.Size);
        var full = graph.View.Lod == GraphLod.Full;

        var edge = Edge();

        if (node.IsSelected)
        {
            // The picked ring, which is a second edge outside the first rather than a
            // thicker one, so the card does not appear to grow when it is picked. The card's
            // own edge is drawn inside its box, so the ring starts where the box ends.
            context.DrawRectangle(
                null,
                graph.PickedHalo,
                new RoundedRect(box.Inflate(1.5), radius + 1.5));
        }

        context.DrawRectangle(off ? colours.NodeOff : colours.Node, null, new RoundedRect(box, radius));

        var header = new Rect(0, 0, box.Width, Math.Min(metrics.HeaderHeight, box.Height));

        // Everything inside the card is clipped to the inside of its own edge. The header is
        // a plain rectangle drawn against that clip rather than a rounded one of its own, so
        // it meets the curve exactly at the top and, on a collapsed node, at the bottom too.
        using (context.PushClip(Inside(box, radius, edge)))
        {
            context.DrawRectangle(off ? colours.HeaderOff : colours.Header, null, header);

            if (!node.IsCollapsed)
            {
                context.DrawLine(colours.Seam, new Point(0, header.Bottom), new Point(box.Width, header.Bottom));
            }

            DrawHeader(context, graph, node, kind, header, full);

            if (!node.IsCollapsed && graph.NodeTemplate is null && node.BodyHeight is null)
            {
                DrawRows(context, graph, node, full);
            }
        }

        // The edge is drawn last, so nothing painted inside the card can take a bite out of
        // it. Drawn first, the header's own fill covered the half of the stroke that falls
        // inside the card and the ring appeared to start below the header.
        context.DrawRectangle(null, edge, Edged(box, radius, edge));

        if (_pins is null)
        {
            DrawPins(context);
        }
    }

    /// <summary>
    /// Draws the card again, pins and all. Invalidating the card alone leaves the pin layer
    /// holding what it drew last, which is how a pin name never appeared under the pointer.
    /// </summary>
    internal void Redraw()
    {
        InvalidateVisual();
        _pins?.InvalidateVisual();
    }

    /// <summary>
    /// Every pin, and the name of the one under the pointer. Drawn by the card itself when
    /// nothing fills its body, and by a layer above when something does.
    /// </summary>
    internal void DrawPins(DrawingContext context)
    {
        if (Graph is not { } graph || Node is not { } node)
        {
            return;
        }

        if (node.IsCollapsed)
        {
            DrawPin(context, graph, node, node.CollapsedPort(PortDirection.Input));
            DrawPin(context, graph, node, node.CollapsedPort(PortDirection.Output));
            return;
        }

        foreach (var port in node.Inputs)
        {
            DrawPin(context, graph, node, port);
        }

        foreach (var port in node.Outputs)
        {
            DrawPin(context, graph, node, port);
        }

        DrawPinName(context, graph, node, graph.View.Lod == GraphLod.Full);
    }

    /// <summary>
    /// The name of the pin under the pointer, beside it. A node laying its pins down its own
    /// edge has no room for names, so resting on one is the only way to read what it is.
    /// </summary>
    private void DrawPinName(DrawingContext context, NodeGraph graph, GraphNode node, bool full)
    {
        if (!full || graph.HoverPort is not { Node: { } owner } port || !ReferenceEquals(owner, node) ||
            node.PortLayout != PortLayout.Edge || port.Name.Length == 0)
        {
            return;
        }

        var colours = graph.Colours;
        var metrics = graph.Metrics;
        var at = metrics.PortOffset(node, port);
        var text = graph.Text.Get(port.Name, GraphTextRole.Label, colours.Label);
        var gap = metrics.PinRadius + 7;

        var box = new Rect(
            port.Direction == PortDirection.Input ? at.X - gap - text.Width - ValuePad * 2 : at.X + gap,
            at.Y - 10,
            text.Width + ValuePad * 2,
            20);

        context.DrawRectangle(colours.Well, colours.WellEdge, new RoundedRect(box, 4));
        context.DrawText(text, new Point(box.X + ValuePad, box.Center.Y - text.Height / 2));
    }

    /// <summary>
    /// Where the edge is stroked. A stroke straddles the shape it is drawn on, so it is set
    /// half a pen in and the whole of it lands inside the card. Centred on the box instead,
    /// half the line falls outside and content filling the body reads as having no edge at
    /// all against it.
    /// </summary>
    private static RoundedRect Edged(Rect box, double radius, IPen edge)
    {
        var half = edge.Thickness / 2;

        return new RoundedRect(box.Deflate(half), Math.Max(0, radius - half));
    }

    /// <summary>
    /// The card inside its own edge. Everything the card draws and everything a template
    /// draws is held to this, so the edge is never painted over.
    /// </summary>
    private static RoundedRect Inside(Rect box, double radius, IPen edge)
    {
        var whole = edge.Thickness;

        return new RoundedRect(box.Deflate(whole), Math.Max(0, radius - whole));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _body?.Measure(BodyBox(availableSize).Size);
        _footer?.Measure(FooterBox(availableSize).Size);
        _pins?.Measure(availableSize);

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The pin layer covers the whole card and is never clipped, since a pin straddles the
        // edge and its name reaches past it.
        _pins?.Arrange(new Rect(finalSize));

        if (_body is null && _footer is null)
        {
            return finalSize;
        }

        // A template fills the body, and a visual child draws after Render, so without a clip
        // it squares off the card's bottom corners and paints over the edge beside them. The
        // shape is the inside of the edge, the same one the drawn card is clipped to, moved
        // into each presenter's own coordinates.
        var inside = Inside(new Rect(finalSize), Graph?.Metrics.NodeRadius ?? 0, Edge());

        Place(_body, BodyBox(finalSize), inside);
        Place(_footer, FooterBox(finalSize), inside);

        return finalSize;
    }

    private static void Place(ContentPresenter? presenter, Rect where, RoundedRect inside)
    {
        if (presenter is null)
        {
            return;
        }

        presenter.Arrange(where);
        presenter.Clip = new RectangleGeometry(
            inside.Rect.Translate(new Vector(0, -where.Y)),
            inside.RadiiTopLeft.X,
            inside.RadiiTopLeft.Y);
    }

    private NodePins Over()
    {
        if (_pins is not null)
        {
            return _pins;
        }

        var layer = new NodePins(this) { ZIndex = 1, IsHitTestVisible = false, ClipToBounds = false };

        VisualChildren.Add(layer);
        return layer;
    }

    private NodePins? Under()
    {
        if (_pins is not null)
        {
            VisualChildren.Remove(_pins);
        }

        return null;
    }

    /// <summary>The strip under the body, or nothing when the node asked for none.</summary>
    private Rect FooterBox(Size size)
    {
        if (Node is not { FooterHeight: > 0 } node)
        {
            return default;
        }

        return new Rect(0, Math.Max(0, size.Height - node.FooterHeight), size.Width, node.FooterHeight);
    }

    /// <summary>
    /// Puts a presenter in a slot, or takes it out again. Everything a container is told is
    /// told here, since a card is handed a different node without being built again.
    /// </summary>
    private ContentPresenter? Slot(ContentPresenter? presenter, IDataTemplate? template, GraphNode node)
    {
        if (template is null)
        {
            if (presenter is not null)
            {
                LogicalChildren.Remove(presenter);
                VisualChildren.Remove(presenter);
            }

            return null;
        }

        presenter ??= Add();

        // The content goes first. A presenter told its template while it still holds nothing
        // builds the template against null, and an explicitly set ContentTemplate is used
        // without being matched against the data first.
        presenter.Content = node;
        presenter.ContentTemplate = template;

        return presenter;
    }

    /// <summary>The pen the card's edge is drawn with, whatever state it is in.</summary>
    private IPen Edge()
    {
        if (Graph is not { } graph || Node is not { } node)
        {
            return new Pen(Brushes.Transparent);
        }

        return node.IsSelected
            ? graph.Colours.NodeEdgePicked
            : node.HasProblem
                ? graph.Colours.NodeEdgeProblem
                : graph.Colours.NodeEdge;
    }

    private Rect BodyBox(Size size)
    {
        if (Graph is not { } graph || Node is not { } node)
        {
            return default;
        }

        var top = graph.Metrics.HeaderHeight;
        var height = Math.Max(0, size.Height - top - node.FooterHeight);

        return new Rect(0, top, size.Width, height);
    }

    private ContentPresenter Add()
    {
        var presenter = new ContentPresenter { ZIndex = 0 };

        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
        return presenter;
    }

    private void DrawHeader(DrawingContext context, NodeGraph graph, GraphNode node, GraphKind kind, Rect header, bool full)
    {
        var colours = graph.Colours;
        var off = node.IsBypassed;
        var middle = header.Center.Y;
        var at = HeaderLead;

        if (kind.Icon is { } glyph && graph.Glyph(glyph) is { } geometry)
        {
            DrawGlyph(context, geometry, off ? colours.Muted : kind.Ink, at, middle, IconSize);
            at += IconSize + Gap;
        }

        var tail = header.Width - 6;

        // The caret takes its room first, so a long title trims rather than running under it.
        if (graph.Glyph(node.IsCollapsed ? IconGlyph.ChevronRight : IconGlyph.ChevronDown) is { } caret)
        {
            tail -= CaretSize;
            DrawGlyph(context, caret, colours.Muted, tail, middle, CaretSize);
            tail -= Gap;
        }

        if (full && node.Badge is { Length: > 0 } badge)
        {
            var text = graph.Text.Get(badge, GraphTextRole.Badge, off ? colours.Warn : colours.AccentInk);
            var width = text.Width + ValuePad * 2;
            var box = new Rect(tail - width, middle - 9, width, 18);

            context.DrawRectangle(colours.AccentTint, colours.AccentTintEdge, new RoundedRect(box, 4));
            context.DrawText(text, new Point(box.X + ValuePad, middle - text.Height / 2));

            tail = box.X - Gap;
        }

        var room = tail - at;

        if (room > 6)
        {
            var title = graph.Text.Get(node.Title, GraphTextRole.Title, off ? colours.Muted : colours.Title, room);

            context.DrawText(title, new Point(at, middle - title.Height / 2));
        }
    }

    private void DrawRows(DrawingContext context, NodeGraph graph, GraphNode node, bool full)
    {
        if (!full)
        {
            return;
        }

        var metrics = graph.Metrics;
        var colours = graph.Colours;
        var model = graph.Model;
        var off = node.IsBypassed;
        var ink = off ? colours.Muted : colours.Label;

        foreach (var port in node.Inputs)
        {
            var middle = metrics.HeaderHeight + metrics.BodyPadding + metrics.RowHeight * port.Index + metrics.RowHeight / 2;
            var wired = model?.IsWired(port) ?? false;
            var at = RowLead;

            var name = graph.Text.Get(port.Name, GraphTextRole.Label, ink);

            context.DrawText(name, new Point(at, middle - name.Height / 2));
            at += name.Width + Gap;

            if (wired)
            {
                continue;
            }

            if (port.Value is { Length: > 0 } value)
            {
                DrawValue(context, graph, value, at, middle);
            }
            else if (graph.Glyph(IconGlyph.AlertCircle) is { } mark)
            {
                DrawGlyph(context, mark, colours.Error, at, middle, MarkSize);
            }
        }

        foreach (var port in node.Outputs)
        {
            var middle = metrics.HeaderHeight + metrics.BodyPadding + metrics.RowHeight * port.Index + metrics.RowHeight / 2;
            var name = graph.Text.Get(port.Name, GraphTextRole.Label, ink);

            context.DrawText(name, new Point(Bounds.Width - RowLead - name.Width, middle - name.Height / 2));
        }
    }

    private static void DrawValue(DrawingContext context, NodeGraph graph, string value, double at, double middle)
    {
        var colours = graph.Colours;
        var text = graph.Text.Get(value, GraphTextRole.Value, colours.Value);
        var box = new Rect(at, middle - 9, text.Width + ValuePad * 2, 18);

        context.DrawRectangle(colours.Well, colours.WellEdge, new RoundedRect(box, 4));
        context.DrawText(text, new Point(box.X + ValuePad, middle - text.Height / 2));
    }

    private void DrawPin(DrawingContext context, NodeGraph graph, GraphNode node, GraphPort? port)
    {
        if (port is null)
        {
            return;
        }

        var metrics = graph.Metrics;
        var colours = graph.Colours;
        var at = metrics.PortOffset(node, port);
        var ink = graph.Ports.Brush(port.Type);
        var wired = graph.Model?.IsWired(port) ?? false;
        var state = graph.PortState(port);

        var fill = wired || state == PortState.Allowed ? ink : colours.Node;

        var ring = state switch
        {
            PortState.Allowed => colours.Ok,
            PortState.Refused => colours.Error,
            _ => ink,
        };

        if (state != PortState.Rest)
        {
            context.DrawEllipse(
                state == PortState.Allowed ? graph.PinGlowOk : graph.PinGlowRefused,
                null,
                at,
                metrics.PinRadius + 4,
                metrics.PinRadius + 4);
        }

        var pen = graph.PinPen(ring);
        var reach = metrics.PinRadius;

        if (port.Shape == PortShape.Round)
        {
            context.DrawEllipse(fill, pen, at, reach, reach);
            return;
        }

        // A diamond is the square turned on its corner, so both shapes are one box and the
        // pin keeps the same reach whichever it is drawn as.
        using var _ = port.Shape == PortShape.Diamond
            ? context.PushTransform(
                Matrix.CreateTranslation(-at.X, -at.Y) *
                Matrix.CreateRotation(Math.PI / 4) *
                Matrix.CreateTranslation(at.X, at.Y))
            : default;

        context.DrawRectangle(
            fill,
            pen,
            new RoundedRect(new Rect(at.X - reach, at.Y - reach, reach * 2, reach * 2), 1.5));
    }

    private static void DrawGlyph(DrawingContext context, Geometry geometry, IBrush ink, double left, double middle, double size)
    {
        var scale = size / IconBox;

        using var _ = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(left, middle - size / 2));

        context.DrawGeometry(ink, null, geometry);
    }
}

/// <summary>What a pin is saying while a wire is being dragged.</summary>
public enum PortState
{
    Rest,

    /// <summary>Under the pointer and the wire may land here.</summary>
    Allowed,

    /// <summary>Under the pointer and the wire may not.</summary>
    Refused,
}
