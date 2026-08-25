using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A frame, drawn. The label tab sits above the box, so the card is arranged taller than the
/// frame's own bounds by <see cref="Lead"/> and everything inside it is shifted down by that.
/// </summary>
public sealed class GraphFrameCard : Control
{
    private const double LabelPad = 9;
    private const double LabelGap = 3;

    internal NodeGraph? Graph { get; private set; }

    internal GraphFrame? Frame { get; private set; }

    /// <summary>How far above the frame's own box the card starts.</summary>
    internal double Lead => (Graph?.Metrics.FrameLabelHeight ?? 20) + LabelGap;

    internal void Follow(NodeGraph graph, GraphFrame frame)
    {
        Graph = graph;
        Frame = frame;
        ZIndex = frame.Layer;
        InvalidateVisual();
    }

    internal void Release()
    {
        Graph = null;
        Frame = null;
    }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph || Frame is not { } frame)
        {
            return;
        }

        var metrics = graph.Metrics;
        var radius = metrics.FrameRadius;
        var lead = Lead;
        var box = new Rect(0, lead, Bounds.Width, Math.Max(0, Bounds.Height - lead));

        var held = frame.IsSelected || ReferenceEquals(frame, graph.HoverFrame);

        // The edge is what a person grabs to resize, so it comes forward when it is picked or
        // when the pointer is on it. At rest it is a boundary and nothing more.
        var edge = frame.IsSelected
            ? graph.AccentColour
            : held ? Sink(frame.Colour, 0.72) : Sink(frame.Colour, 0.42);

        var pen = graph.FramePen(edge);

        context.DrawRectangle(graph.Colours.FrameWash, pen, new RoundedRect(box, radius));

        if (graph.View.Lod == GraphLod.Block)
        {
            return;
        }

        if (held)
        {
            Handles(context, graph, box, edge);
        }

        var text = graph.Text.Get(frame.Label ?? string.Empty, GraphTextRole.Label, Lift(frame.Colour));
        var tab = new Rect(0, 0, text.Width + LabelPad * 2, metrics.FrameLabelHeight);

        context.DrawRectangle(new ImmutableSolidColorBrush(Sink(frame.Colour, 0.42)), null, new RoundedRect(tab, 6));
        context.DrawText(text, new Point(LabelPad, tab.Center.Y - text.Height / 2));
    }

    /// <summary>
    /// The four corner marks. They say the edge can be pulled, which nothing else does, and
    /// they hold one size on screen rather than growing with the zoom.
    /// </summary>
    private static void Handles(DrawingContext context, NodeGraph graph, Rect box, Color edge)
    {
        var reach = 7 / graph.View.Zoom;
        var ink = new ImmutableSolidColorBrush(edge);

        foreach (var corner in (ReadOnlySpan<Point>)[box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight])
        {
            context.DrawRectangle(
                ink,
                null,
                new RoundedRect(
                    new Rect(corner.X - reach / 2, corner.Y - reach / 2, reach, reach),
                    reach / 3));
        }
    }

    /// <summary>The frame's colour taken down towards the ground, which is its edge.</summary>
    private static Color Sink(Color colour, double amount) =>
        Color.FromArgb(
            255,
            (byte)(colour.R * amount + 0x16 * (1 - amount)),
            (byte)(colour.G * amount + 0x17 * (1 - amount)),
            (byte)(colour.B * amount + 0x19 * (1 - amount)));

    /// <summary>The frame's colour brought up towards white, which is its label ink.</summary>
    private static IBrush Lift(Color colour) =>
        new ImmutableSolidColorBrush(Color.FromArgb(
            255,
            (byte)(colour.R + (255 - colour.R) * 0.45),
            (byte)(colour.G + (255 - colour.G) * 0.45),
            (byte)(colour.B + (255 - colour.B) * 0.45)));
}
