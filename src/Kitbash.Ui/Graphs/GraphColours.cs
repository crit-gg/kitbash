using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Every brush and pen the canvas draws with, resolved from the tokens once and kept. A
/// resource lookup per node per frame is the cost this exists to avoid.
/// </summary>
public sealed class GraphColours
{
    private static readonly IBrush Missing = new ImmutableSolidColorBrush(Colors.Magenta);

    private static readonly IPen NoPen = new ImmutablePen(Brushes.Magenta, 1);

    private GraphColours()
    {
    }

    public IBrush Ground { get; private init; } = Missing;

    public IBrush Grid { get; private init; } = Missing;

    public IBrush Node { get; private init; } = Missing;

    public IBrush Header { get; private init; } = Missing;

    public IBrush NodeOff { get; private init; } = Missing;

    public IBrush HeaderOff { get; private init; } = Missing;

    public IBrush Title { get; private init; } = Missing;

    public IBrush Label { get; private init; } = Missing;

    public IBrush Muted { get; private init; } = Missing;

    public IBrush Value { get; private init; } = Missing;

    public IBrush Well { get; private init; } = Missing;

    public IBrush Accent { get; private init; } = Missing;

    public IBrush AccentInk { get; private init; } = Missing;

    public IBrush AccentTint { get; private init; } = Missing;

    public IBrush AccentTintLine { get; private init; } = Missing;

    public IPen AccentTintEdge { get; private init; } = NoPen;

    public IBrush Ok { get; private init; } = Missing;

    public IBrush Error { get; private init; } = Missing;

    public IBrush Warn { get; private init; } = Missing;

    public IBrush FrameWash { get; private init; } = Missing;

    public IBrush Marquee { get; private init; } = Missing;

    public IPen NodeEdge { get; private init; } = NoPen;

    public IPen NodeEdgeProblem { get; private init; } = NoPen;

    public IPen NodeEdgePicked { get; private init; } = NoPen;

    public IPen Seam { get; private init; } = NoPen;

    public IPen WellEdge { get; private init; } = NoPen;

    public IPen MarqueeEdge { get; private init; } = NoPen;

    /// <summary>The colour a wire takes while it is picked or one of its nodes is.</summary>
    public IBrush WireActive { get; private init; } = Missing;

    /// <summary>A wire whose node is bypassed.</summary>
    public IBrush WireOff { get; private init; } = Missing;

    /// <summary>The five spare port colours, for a type nothing was said about.</summary>
    public IReadOnlyList<IBrush> Pins { get; private init; } = [];

    /// <summary>
    /// What a new frame is tinted. They are cycled so two frames made one after another do
    /// not come out the same colour, which is the whole reason a frame carries one.
    /// </summary>
    public IReadOnlyList<Color> Frames { get; private init; } = [];

    /// <summary>
    /// Reads the tokens off whatever the control is in. Call it again when the theme variant
    /// changes, since a brush resolved once would be the old variant's.
    /// </summary>
    public static GraphColours Resolve(Control host)
    {
        return new GraphColours
        {
            Ground = Brush(host, "GraphGround"),
            Grid = Brush(host, "GraphGrid"),
            Node = Brush(host, "SurfaceNest2"),
            Header = Brush(host, "SurfaceNest3"),
            NodeOff = Brush(host, "GraphNodeOff"),
            HeaderOff = Brush(host, "GraphHeaderOff"),
            Title = Brush(host, "InkTitle"),
            Label = Brush(host, "InkSecondary"),
            Muted = Brush(host, "InkMuted"),
            Value = Brush(host, "InkSecondary"),
            Well = Brush(host, "SurfaceWell"),
            Accent = Brush(host, "Accent"),
            AccentInk = Brush(host, "AccentTintInk"),
            AccentTint = Brush(host, "SurfaceDrop"),
            AccentTintLine = Brush(host, "AccentTintLine"),
            AccentTintEdge = Pen(host, "AccentTintLine"),
            Ok = Brush(host, "Ok"),
            Error = Brush(host, "Error"),
            Warn = Brush(host, "Warn"),
            FrameWash = Brush(host, "GraphFrameWash"),
            Marquee = Brush(host, "GraphMarquee"),
            NodeEdge = Pen(host, "LineControl"),
            NodeEdgeProblem = Pen(host, "GraphNodeProblemLine"),
            NodeEdgePicked = Pen(host, "Accent"),
            Seam = Pen(host, "LineSeam"),
            WellEdge = Pen(host, "LineControl"),
            MarqueeEdge = Pen(host, "Accent"),
            WireActive = Brush(host, "SelectionInk"),
            WireOff = Brush(host, "LineControl"),
            Frames =
            [
                Ink(host, "Accent"),
                Ink(host, "PinEnum"),
                Ink(host, "PinFloat"),
                Ink(host, "Warn"),
                Ink(host, "PinBool"),
            ],
            Pins =
            [
                Brush(host, "PinFloat"),
                Brush(host, "PinInt"),
                Brush(host, "PinBool"),
                Brush(host, "PinEnum"),
                Brush(host, "PinStruct"),
            ],
        };
    }

    private static IBrush Brush(Control host, string key) =>
        host.TryFindResource(key, out var found) && found is IBrush brush ? brush : Missing;

    private static IPen Pen(Control host, string key) => new ImmutablePen(Brush(host, key).ToImmutable(), 1);

    private static Color Ink(Control host, string key) =>
        Brush(host, key) is ISolidColorBrush solid ? solid.Color : Colors.White;
}
