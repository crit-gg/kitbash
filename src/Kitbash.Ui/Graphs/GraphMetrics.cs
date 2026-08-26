using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The geometry a node, a wire and the minimap all read. Every value is in graph units,
/// which are device independent pixels at a zoom of one.
/// </summary>
public sealed class GraphMetrics
{
    /// <summary>The dense set, which is what both designs draw.</summary>
    public static GraphMetrics Dense { get; } = new();

    /// <summary>The comfortable set, for a graph that is browsed rather than authored.</summary>
    public static GraphMetrics Comfortable { get; } = new()
    {
        HeaderHeight = 32,
        RowHeight = 25,
        BodyPadding = 10,
    };

    /// <summary>The title strip at the top of a node.</summary>
    public double HeaderHeight { get; init; } = 28;

    /// <summary>One port row.</summary>
    public double RowHeight { get; init; } = 22;

    /// <summary>Above the first port row and below the last.</summary>
    public double BodyPadding { get; init; } = 8;

    /// <summary>The drawn radius of a pin. The design draws a 9px dot.</summary>
    public double PinRadius { get; init; } = 4.5;

    /// <summary>The ring around a pin.</summary>
    public double PinRing { get; init; } = 1.5;

    /// <summary>How far from a pin's centre a press still counts as that pin.</summary>
    public double PinReach { get; init; } = 11;

    /// <summary>How far from a wire a press still counts as that wire.</summary>
    public double WireReach { get; init; } = 7;

    /// <summary>The dot grid step.</summary>
    public double GridStep { get; init; } = 24;

    /// <summary>What a node lands on when snapping is on.</summary>
    public double SnapStep { get; init; } = 12;

    /// <summary>
    /// How far a press has to travel before it is a drag. Under this nothing moves, so a
    /// click that wobbles picks a node rather than nudging it out of place.
    /// </summary>
    public double DragReach { get; init; } = 9;

    /// <summary>The node corner.</summary>
    public double NodeRadius { get; init; } = 8;

    /// <summary>The frame corner, and the label tab above it.</summary>
    public double FrameRadius { get; init; } = 8;

    /// <summary>The label tab a frame carries above its top left corner.</summary>
    public double FrameLabelHeight { get; init; } = 20;

    /// <summary>Between the tab and the box under it.</summary>
    public double FrameLabelGap { get; init; } = 3;

    /// <summary>The narrowest a tab is drawn, so a short name is still something to aim at.</summary>
    public double FrameLabelWidest { get; init; } = 96;

    /// <summary>How far either side of a frame's edge a press counts as a resize.</summary>
    public double FrameEdgeReach { get; init; } = 7;

    /// <summary>The smallest a frame can be dragged down to.</summary>
    public double FrameSmallest { get; init; } = 80;

    /// <summary>What a frame made around a selection leaves around it.</summary>
    public double FramePadding { get; init; } = 22;

    /// <summary>The reroute handle's drawn radius.</summary>
    public double RerouteRadius { get; init; } = 6;

    /// <summary>What a fitted graph leaves around itself, in screen pixels.</summary>
    public double FitMargin { get; init; } = 46;

    /// <summary>The width of a wire at a zoom of one.</summary>
    public double WireWidth { get; init; } = 1.8;

    /// <summary>The width of a wire that is picked or whose node is.</summary>
    public double WireWidthActive { get; init; } = 2.5;

    /// <summary>
    /// The shortest horizontal reach of a bezier's control point. Below it a wire between
    /// two pins nearly in line bows the wrong way.
    /// </summary>
    public double WireBow { get; init; } = 46;

    /// <summary>How far a node's box is grown to hold the wire bulge, for culling.</summary>
    public double CullPad { get; init; } = 64;

    /// <summary>The cell of the spatial index. Wide enough that a node lands in one or two.</summary>
    public double IndexCell { get; init; } = 256;

    /// <summary>Where a port's pin sits inside a node, relative to the node's own corner.</summary>
    public Point PortOffset(GraphNode node, GraphPort port)
    {
        var x = port.Direction == PortDirection.Input ? 0 : node.Width;

        if (node.IsCollapsed)
        {
            return new Point(x, HeaderHeight / 2);
        }

        // PortLayout answers Edge on its own for a node with a body of its own, so this is
        // the only test needed here.
        if (node.PortLayout == PortLayout.Edge)
        {
            var count = node.PortCount(port.Direction);
            var top = HeaderHeight;
            var span = node.Height - HeaderHeight - node.FooterHeight;

            return new Point(x, top + span * (port.Index + 1) / (count + 1));
        }

        return new Point(x, HeaderHeight + BodyPadding + RowHeight * port.Index + RowHeight / 2);
    }
}
