using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The whole graph small, with the viewport drawn on it. A press moves the view there.
///
/// The graph itself costs the number of nodes to draw, and a pan would pay that every frame,
/// so it is drawn once into a bitmap and only the viewport rectangle moves after that. The
/// bitmap is thrown away when the graph changes, when the map is resized or when the theme is.
/// </summary>
public class NodeMinimap : Control
{
    public static readonly StyledProperty<NodeGraph?> GraphProperty =
        AvaloniaProperty.Register<NodeMinimap, NodeGraph?>(nameof(Graph));

    /// <summary>What the graph is inset by inside the map.</summary>
    public static readonly StyledProperty<double> PadProperty =
        AvaloniaProperty.Register<NodeMinimap, double>(nameof(Pad), 9d);

    private static readonly Cursor Pointing = new(StandardCursorType.Hand);
    private static readonly Cursor Grabbing = new(StandardCursorType.SizeAll);

    private RenderTargetBitmap? _map;
    private Size _mapped;
    private double _scale;
    private Point _offset;
    private bool _dragging;
    private Vector _grabbed;

    public NodeMinimap()
    {
        Cursor = Pointing;
    }

    public NodeGraph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public double Pad
    {
        get => GetValue(PadProperty);
        set => SetValue(PadProperty, value);
    }

    /// <summary>Whether the view is being dragged about on the map.</summary>
    public bool IsDragging => _dragging;

    public override void Render(DrawingContext context)
    {
        if (Graph is not { Model: not null } graph || !Place())
        {
            return;
        }

        Build(graph);

        if (_map is not null)
        {
            context.DrawImage(_map, new Rect(Bounds.Size));
        }

        var seen = graph.View.Viewport(graph.Bounds.Size);

        context.DrawRectangle(
            graph.Colours.Marquee,
            graph.Colours.MarqueeEdge,
            new RoundedRect(
                new Rect(Map(seen.TopLeft), new Size(seen.Width * _scale, seen.Height * _scale)),
                2));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            Forget();
            return;
        }

        if (change.Property != GraphProperty)
        {
            return;
        }

        if (change.OldValue is NodeGraph old)
        {
            old.View.Changed -= OnMoved;
            old.Selection.Changed -= OnStale;
            old.GraphChanged -= OnStale;
        }

        if (change.NewValue is NodeGraph graph)
        {
            graph.View.Changed += OnMoved;
            graph.Selection.Changed += OnStale;
            graph.GraphChanged += OnStale;
        }

        Forget();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Graph is not { Model: not null } graph || !Place())
        {
            return;
        }

        var at = e.GetPosition(this);
        var seen = graph.View.Viewport(graph.Bounds.Size);
        var box = new Rect(Map(seen.TopLeft), new Size(seen.Width * _scale, seen.Height * _scale));

        // Pressing inside the viewport takes hold of it where it was grabbed, so it does not
        // jump out from under the pointer. Pressing outside it goes there first, which is
        // what a press on a map is for.
        _grabbed = box.Contains(at) ? box.Center - at : default;
        _dragging = true;

        e.Pointer.Capture(this);
        Cursor = Grabbing;
        Move(graph, at);

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging || Graph is not { Model: not null } graph || !Place())
        {
            return;
        }

        Move(graph, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        Let(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _dragging = false;
        Cursor = Pointing;
    }

    private void Let(IPointer pointer)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        pointer.Capture(null);
        Cursor = Pointing;
    }

    private void Move(NodeGraph graph, Point at)
    {
        var wanted = at + _grabbed;

        graph.View.CentreOn(
            new Point((wanted.X - _offset.X) / _scale, (wanted.Y - _offset.Y) / _scale),
            graph.Bounds.Size);
    }

    /// <summary>
    /// Works out the scale and the offset the whole graph fits at, and says whether there is
    /// anything to draw at all.
    /// </summary>
    private bool Place()
    {
        if (Graph?.Model is not { } model || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return false;
        }

        var content = model.ContentBounds;
        var pad = Pad;

        if (content.Width <= 0 || content.Height <= 0)
        {
            return false;
        }

        _scale = Math.Min((Bounds.Width - pad * 2) / content.Width, (Bounds.Height - pad * 2) / content.Height);

        if (_scale <= 0 || double.IsInfinity(_scale))
        {
            return false;
        }

        _offset = new Point(
            (Bounds.Width - content.Width * _scale) / 2 - content.X * _scale,
            (Bounds.Height - content.Height * _scale) / 2 - content.Y * _scale);

        return true;
    }

    private void Build(NodeGraph graph)
    {
        if (_map is not null && _mapped == Bounds.Size)
        {
            return;
        }

        var model = graph.Model!;
        var colours = graph.Colours;

        _map?.Dispose();
        _map = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height)));
        _mapped = Bounds.Size;

        using var context = _map.CreateDrawingContext();

        var wire = new ImmutablePen(colours.WireOff.ToImmutable(), 1);

        foreach (var link in model.Links)
        {
            context.DrawLine(wire, Map(link.FromNode.Bounds.TopRight), Map(link.ToNode.Bounds.TopLeft));
        }

        foreach (var node in model.Nodes)
        {
            var box = new Rect(
                Map(node.Bounds.TopLeft),
                new Size(Math.Max(2, node.Width * _scale), Math.Max(1.5, node.Height * _scale)));

            var ink = node.IsSelected
                ? colours.Accent
                : node.IsBypassed
                    ? colours.WireOff
                    : colours.Muted;

            context.DrawRectangle(ink, null, new RoundedRect(box, 1.5));
        }
    }

    private Point Map(Point at) => new(at.X * _scale + _offset.X, at.Y * _scale + _offset.Y);

    private void Forget()
    {
        _map?.Dispose();
        _map = null;
        InvalidateVisual();
    }

    private void OnStale(object? sender, EventArgs e) => Forget();

    private void OnMoved(object? sender, EventArgs e) => InvalidateVisual();
}
