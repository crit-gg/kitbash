using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The box select band and the cut stroke. Both are screen space and over everything, which
/// is why they are a layer of their own rather than part of the wires.
/// </summary>
public sealed class GraphMarquee : Control
{
    private static readonly ImmutableDashStyle Dashes = new([3, 3], 0);

    private IPen? _knife;

    internal NodeGraph? Graph { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        if (graph.Marquee is { } band && (band.Width > 0 || band.Height > 0))
        {
            context.DrawRectangle(graph.Colours.Marquee, graph.Colours.MarqueeEdge, new RoundedRect(band, 3));
        }

        if (graph.CutStroke is { } stroke)
        {
            _knife ??= new ImmutablePen(graph.Colours.Error.ToImmutable(), 1.5, Dashes);

            context.DrawLine(_knife, stroke.From, stroke.To);
        }
    }

    /// <summary>Drops the pen, which a theme change makes stale.</summary>
    internal void Forget()
    {
        _knife = null;
        InvalidateVisual();
    }
}
