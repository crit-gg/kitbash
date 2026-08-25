using Avalonia;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A link's two ends to a path. Nothing here is per frame: a path is built once and kept on
/// the link until one of its nodes moves.
/// </summary>
public static class WireRouter
{
    /// <summary>The whole wire, through however many points it is dragged through.</summary>
    public static Geometry Build(Point from, Point to, IReadOnlyList<Point> through, WireStyle style, GraphMetrics metrics)
    {
        var geometry = new StreamGeometry();

        using var context = geometry.Open();

        context.BeginFigure(from, false);

        var at = from;

        foreach (var next in through)
        {
            Segment(context, at, next, style, metrics);
            at = next;
        }

        Segment(context, at, to, style, metrics);
        context.EndFigure(false);

        return geometry;
    }

    /// <summary>One length of wire between two points, on its own, for hit testing a run.</summary>
    public static Geometry Build(Point from, Point to, WireStyle style, GraphMetrics metrics)
    {
        var geometry = new StreamGeometry();

        using var context = geometry.Open();

        context.BeginFigure(from, false);
        Segment(context, from, to, style, metrics);
        context.EndFigure(false);

        return geometry;
    }

    /// <summary>
    /// How far a bezier's control point reaches. It never drops below the bow, since two
    /// pins nearly in line would otherwise get a wire that bends backwards.
    /// </summary>
    public static double Reach(Point from, Point to, GraphMetrics metrics) =>
        Math.Max(metrics.WireBow, Math.Abs(to.X - from.X) * 0.5);

    private static void Segment(StreamGeometryContext context, Point from, Point to, WireStyle style, GraphMetrics metrics)
    {
        if (style == WireStyle.Orthogonal)
        {
            var middle = (from.X + to.X) / 2;

            context.LineTo(new Point(middle, from.Y));
            context.LineTo(new Point(middle, to.Y));
            context.LineTo(to);
            return;
        }

        var reach = Reach(from, to, metrics);

        context.CubicBezierTo(
            new Point(from.X + reach, from.Y),
            new Point(to.X - reach, to.Y),
            to);
    }
}
