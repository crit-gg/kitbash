using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Every wire, the one being drawn and the reroute handles. It draws in graph space under
/// one transform, so a pan or a zoom routes nothing again and rebuilds no geometry.
/// </summary>
public sealed class GraphWires : Control
{
    private static readonly ImmutableDashStyle Dashes = new([4, 4], 0);

    // Enough for every port colour at several widths at once.
    private const int Ceiling = 256;

    private readonly Dictionary<(IBrush Ink, double Width, bool Dashed), IPen> _pens = [];
    private readonly List<GraphLink> _seen = [];

    internal NodeGraph? Graph { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { Model: { } model } graph || Bounds.Width <= 0)
        {
            return;
        }

        var view = graph.View;
        var metrics = graph.Metrics;
        var colours = graph.Colours;
        var style = graph.WireStyle;
        var block = view.Lod == GraphLod.Block;

        // A wire leaves the box its two ends make, so the cull area is grown by the bow.
        var area = view.Viewport(Bounds.Size).Inflate(metrics.WireBow);

        using var _ = context.PushTransform(view.Matrix);

        var thin = Math.Max(metrics.WireWidth, 1 / view.Zoom);
        var thick = Math.Max(metrics.WireWidthActive, 1.6 / view.Zoom);

        _seen.Clear();
        model.QueryLinks(area, _seen);

        foreach (var link in _seen)
        {
            // The index answers with what is near, and the box says what is really in view.
            // Neither routes anything, so a graph with two thousand wires builds geometry
            // for the handful that can be seen.
            if (!link.Box(metrics).Intersects(area))
            {
                continue;
            }

            var path = link.Route(style, metrics);

            var off = link.FromNode.IsBypassed || link.ToNode.IsBypassed;
            var active = !block &&
                (link.IsSelected || ReferenceEquals(link, graph.HoverLink) ||
                 link.FromNode.IsSelected || link.ToNode.IsSelected);

            var ink = off
                ? colours.WireOff
                : active
                    ? colours.WireActive
                    : graph.Ports.Brush(link.From.Type);

            context.DrawGeometry(null, Pen(ink, active ? thick : thin, off), path);
        }

        if (block)
        {
            return;
        }

        DrawReroutes(context, graph, model, metrics, area);
        DrawLive(context, graph, metrics, thick);
    }

    /// <summary>Drops every pen, which a theme change makes stale.</summary>
    internal void Forget() => _pens.Clear();

    private void DrawReroutes(DrawingContext context, NodeGraph graph, GraphModel model, GraphMetrics metrics, Rect area)
    {
        var radius = metrics.RerouteRadius;
        var edge = Pen(graph.Colours.Ground, 2 / graph.View.Zoom, false);

        foreach (var link in _seen)
        {
            var ink = link.IsSelected ? graph.Colours.Accent : graph.Ports.Brush(link.From.Type);

            foreach (var at in link.Reroutes)
            {
                if (area.Contains(at))
                {
                    context.DrawEllipse(ink, edge, at, radius, radius);
                }
            }
        }
    }

    private void DrawLive(DrawingContext context, NodeGraph graph, GraphMetrics metrics, double width)
    {
        if (graph.LiveWire is not { } live)
        {
            return;
        }

        var path = WireRouter.Build(live.From, live.To, graph.WireStyle, metrics);
        var ink = live.IsAllowed ? graph.Colours.Ok : live.Ink;

        context.DrawGeometry(null, Pen(ink, width, true), path);
    }

    private IPen Pen(IBrush ink, double width, bool dashed)
    {
        var key = (ink, Math.Round(width, 2), dashed);

        if (_pens.TryGetValue(key, out var found))
        {
            return found;
        }

        // A width is part of the key and a zoom makes a new one, so a long spell of zooming
        // would grow this without end. Past the ceiling the whole set goes and refills.
        if (_pens.Count >= Ceiling)
        {
            _pens.Clear();
        }

        return _pens[key] = new ImmutablePen(
            ink.ToImmutable(),
            key.Item2,
            dashed ? Dashes : null,
            PenLineCap.Round);
    }
}

/// <summary>The wire being dragged off a pin, in graph units.</summary>
public readonly record struct LiveWire(Point From, Point To, IBrush Ink, bool IsAllowed);
