using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Where the graph is looked at from. A pan or a zoom writes this and nothing else, so no
/// layout pass runs and no wire is routed again.
/// </summary>
public sealed class GraphView
{
    /// <summary>The furthest out. Under it a graph is a shape rather than a diagram.</summary>
    public const double MinZoom = 0.08;

    /// <summary>The closest in.</summary>
    public const double MaxZoom = 2.5;

    private Vector _offset;
    private double _zoom = 1;

    public event EventHandler? Changed;

    /// <summary>Where the graph origin sits on screen.</summary>
    public Vector Offset
    {
        get => _offset;
        set => Set(value, _zoom);
    }

    public double Zoom
    {
        get => _zoom;
        set => Set(_offset, value);
    }

    /// <summary>How much of a node is worth drawing at this zoom.</summary>
    public GraphLod Lod => _zoom >= 0.5 ? GraphLod.Full : _zoom >= 0.28 ? GraphLod.Reduced : GraphLod.Block;

    /// <summary>The transform every layer draws under.</summary>
    public Matrix Matrix => Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_offset.X, _offset.Y);

    public Point ToGraph(Point screen) => new((screen.X - _offset.X) / _zoom, (screen.Y - _offset.Y) / _zoom);

    public Point ToScreen(Point graph) => new(graph.X * _zoom + _offset.X, graph.Y * _zoom + _offset.Y);

    /// <summary>What of the graph a viewport of this size can see, in graph units.</summary>
    public Rect Viewport(Size size) =>
        new(ToGraph(default), ToGraph(new Point(size.Width, size.Height)));

    /// <summary>Zooms about a point on screen, so whatever is under the pointer stays there.</summary>
    public void ZoomAt(Point screen, double factor)
    {
        var wanted = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        var step = wanted / _zoom;

        Set(new Vector(screen.X - (screen.X - _offset.X) * step, screen.Y - (screen.Y - _offset.Y) * step), wanted);
    }

    /// <summary>Puts a graph point in the middle of a viewport of this size.</summary>
    public void CentreOn(Point graph, Size viewport) =>
        Offset = new Vector(viewport.Width / 2 - graph.X * _zoom, viewport.Height / 2 - graph.Y * _zoom);

    /// <summary>
    /// Sets the zoom and the offset so a box fills the viewport with a margin around it. An
    /// empty viewport is left alone, since a fit before the first layout would land nowhere.
    /// </summary>
    public void Fit(Rect content, Size viewport, double margin, double ceiling = 1.05)
    {
        if (viewport.Width <= margin * 2 || viewport.Height <= margin * 2 ||
            content.Width <= 0 || content.Height <= 0)
        {
            return;
        }

        var zoom = Math.Clamp(
            Math.Min((viewport.Width - margin * 2) / content.Width, (viewport.Height - margin * 2) / content.Height),
            MinZoom,
            Math.Min(MaxZoom, ceiling));

        Set(
            new Vector(
                (viewport.Width - content.Width * zoom) / 2 - content.X * zoom,
                (viewport.Height - content.Height * zoom) / 2 - content.Y * zoom),
            zoom);
    }

    private void Set(Vector offset, double zoom)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

        if (_offset == offset && _zoom.Equals(zoom))
        {
            return;
        }

        _offset = offset;
        _zoom = zoom;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
