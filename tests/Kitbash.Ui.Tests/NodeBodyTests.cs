using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The composed node: a body an app fills, a strip under it, and pins down the outer edge.
/// This is the shape a material graph needs, and the one the drawn node cannot give it.
/// </summary>
public class NodeBodyTests
{
    [AvaloniaFact]
    public void EdgePinsAreSpreadDownTheSideRatherThanPutInRows()
    {
        var node = new GraphNode("n", "Warp", "flt", 138)
        {
            BodyHeight = 122,
            FooterHeight = 20,
            PortLayout = PortLayout.Edge,
        };

        node.AddInput("Input", "Gray");
        node.AddInput("Intensity", "Gray");
        node.AddOutput("Output", "Gray");

        var metrics = GraphMetrics.Dense;
        var top = metrics.HeaderHeight;
        var span = node.Height - top - node.FooterHeight;

        // Evenly down the body, so two pins sit at a third and two thirds of it.
        Assert.Equal(top + span / 3, metrics.PortOffset(node, node.Inputs[0]).Y, 3);
        Assert.Equal(top + span * 2 / 3, metrics.PortOffset(node, node.Inputs[1]).Y, 3);
        Assert.Equal(top + span / 2, metrics.PortOffset(node, node.Outputs[0]).Y, 3);

        // Both sides sit on the node's own edges, which is where a wire ends.
        Assert.Equal(0, metrics.PortOffset(node, node.Inputs[0]).X);
        Assert.Equal(node.Width, metrics.PortOffset(node, node.Outputs[0]).X);
    }

    [AvaloniaFact]
    public void AFooterExistsOnlyWhenTheNodeLeavesRoomForOne()
    {
        var graph = Open(out var window, out var withStrip, out var without);

        try
        {
            Assert.Equal(2, Cards(graph).Count);

            Assert.Equal(2, Slots(graph, withStrip).Count);
            Assert.Single(Slots(graph, without));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheBodyStopsWhereTheFooterStarts()
    {
        var graph = Open(out var window, out var node, out _);

        try
        {
            var slots = Slots(graph, node);
            var body = slots[0].Bounds;
            var strip = slots[1].Bounds;

            Assert.Equal(GraphMetrics.Dense.HeaderHeight, body.Y, 3);
            Assert.Equal(strip.Y, body.Bottom, 3);
            Assert.Equal(node.FooterHeight, strip.Height, 3);
            Assert.Equal(node.Height, strip.Bottom, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADiamondPinIsEmptyAtTheCornersWhereASquareIsFilled()
    {
        var square = Corner(PortShape.Square, out var one);
        var diamond = Corner(PortShape.Diamond, out var two);

        one();
        two();

        // The two shapes fill the same box, so the corner of that box is the one place they
        // have to disagree. Colour alone says a type and this says it again.
        Assert.True(square > 40, $"a square pin left its corner empty, at {square}");
        Assert.True(diamond < 20, $"a diamond pin filled its corner, at {diamond}");
    }

    [AvaloniaFact]
    public void ACollapsedNodeDropsItsBodyAndItsStrip()
    {
        var graph = Open(out var window, out var node, out _);

        try
        {
            Assert.Equal(2, Slots(graph, node).Count);

            node.IsCollapsed = true;
            Settle(window);

            // Left in place the strip is laid out against the collapsed height and covers the
            // very header the node collapsed to.
            Assert.Empty(Slots(graph, node));
            Assert.Equal(GraphMetrics.Dense.HeaderHeight, node.Height, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PinsAreDrawnOverWhateverFillsTheBody()
    {
        var model = new GraphModel();
        var node = new GraphNode("n", "Filled", "gen", 170) { BodyHeight = 120 };
        var feed = new GraphNode("f", "Feed", "gen", 60);

        node.MoveTo(60, 40);
        node.AddInput("Input", "Gray");
        feed.MoveTo(200, 200);
        feed.AddOutput("Out", "Gray");
        model.Add(node);
        model.Add(feed);

        // An unwired pin is drawn in the card's own fill with only its ring in the type's
        // colour, so it is wired here and the whole pin reads as one tone.
        model.Add(new GraphLink(feed.Outputs[0], node.Inputs[0]));

        var graph = new NodeGraph
        {
            Model = model,
            ShowGrid = false,
            Kinds = new GraphKinds(new GraphKind(null, Brushes.White)),
            Ports = new PortPalette([Brushes.Lime]),

            // A body that reaches the card's own edge, which is what a preview does.
            NodeTemplate = new FuncDataTemplate<GraphNode>((_, _) => new Border { Background = Brushes.DarkBlue }),
        };

        var window = new Window { Width = 320, Height = 220, Content = graph };

        window.Show();
        Settle(window);

        graph.View.Zoom = 1;
        graph.View.Offset = default;
        Settle(window);

        try
        {
            var at = node.PortPoint(node.Inputs[0]);
            var shot = window.CaptureRenderedFrame()!;

            using var buffer = shot.Lock();

            var bytes = new byte[buffer.RowBytes * buffer.Size.Height];

            Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            // Inside the pin, on the side the body covers. A visual child draws after its
            // parent's Render, so without a layer above it the body takes this half of every
            // pin on the edge it fills.
            var inside = (int)(at.X + GraphMetrics.Dense.PinRadius * 0.6);
            var pixel = BitConverter.ToInt32(bytes, (int)at.Y * buffer.RowBytes + inside * 4);

            Assert.True(
                ((pixel >> 8) & 0xff) > ((pixel >> 16) & 0xff) + 40,
                "the body painted over the pin");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RestingOnAnEdgePinShowsItsName()
    {
        var model = new GraphModel();
        var node = new GraphNode("n", "Filled", "gen", 170) { BodyHeight = 120 };

        node.MoveTo(60, 60);
        node.AddOutput("Intensity", "Gray");
        model.Add(node);

        var graph = new NodeGraph
        {
            Model = model,
            ShowGrid = false,
            Kinds = new GraphKinds(new GraphKind(null, Brushes.White)),
            Ports = new PortPalette([Brushes.MediumAquamarine]),
            NodeTemplate = new FuncDataTemplate<GraphNode>((_, _) => new Border { Background = Brushes.DimGray }),
        };

        var window = new Window { Width = 420, Height = 260, Content = graph };

        window.Show();
        Settle(window);

        graph.View.Zoom = 1;
        graph.View.Offset = default;
        Settle(window);

        try
        {
            var at = node.PortPoint(node.Outputs[0]);

            Assert.True(Words(window, at) == 0, "a name showed before the pointer was on the pin");

            window.MouseMove(at);
            Settle(window);

            // A node laying its pins down its own edge has no room for names, so resting on
            // one is the only way to read what it is.
            Assert.True(Words(window, at) > 6, "no name showed under the pointer");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>How many light pixels sit in the strip a pin's name is drawn in.</summary>
    private static int Words(Window window, Point pin)
    {
        var shot = window.CaptureRenderedFrame()!;

        using var buffer = shot.Lock();

        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];

        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        var found = 0;

        for (var y = (int)pin.Y - 8; y < (int)pin.Y + 8; y++)
        {
            for (var x = (int)pin.X + 12; x < (int)pin.X + 80; x++)
            {
                var pixel = BitConverter.ToInt32(bytes, y * buffer.RowBytes + x * 4);

                // The ink a name is written in, well above the ground and the chip it sits on.
                if ((pixel & 0xff) > 120)
                {
                    found++;
                }
            }
        }

        return found;
    }

    /// <summary>How much pin colour sits at the corner of a pin's own box.</summary>
    private static int Corner(PortShape shape, out Action close)
    {
        var model = new GraphModel();
        var node = new GraphNode("n", "A node", "math", 170) { PortLayout = PortLayout.Edge };

        node.MoveTo(60, 40);
        node.Add(new GraphPort("Out", "Gray", PortDirection.Output) { Shape = shape });
        model.Add(node);

        var graph = new NodeGraph
        {
            Model = model,
            ShowGrid = false,
            Kinds = new GraphKinds(new GraphKind(null, Brushes.White)),

            // A pin nothing is wired to draws its ring in the type's colour and its middle in
            // the card's, so the wire makes the whole shape read as one tone.
            Ports = new PortPalette([Brushes.Lime]),
        };

        var window = new Window { Width = 320, Height = 200, Content = graph };

        window.Show();
        Settle(window);

        graph.View.Zoom = 1;
        graph.View.Offset = default;
        Settle(window);

        var at = node.PortPoint(node.Outputs[0]);
        // The corner of the pin's own box. A square reaches it, and a diamond, which is that
        // same square turned on its corner, has its edge well inside it there.
        var reach = GraphMetrics.Dense.PinRadius * 0.95;
        var shot = window.CaptureRenderedFrame()!;

        using var buffer = shot.Lock();

        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];

        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        var pixel = BitConverter.ToInt32(bytes, (int)(at.Y + reach) * buffer.RowBytes + (int)(at.X + reach) * 4);

        close = window.Close;

        // Lime against everything else on the canvas, so the green channel alone tells them
        // apart without caring what is behind it.
        return ((pixel >> 8) & 0xff) - (pixel & 0xff);
    }

    private static List<NodeCard> Cards(NodeGraph graph) =>
        [.. graph.GetVisualDescendants().OfType<NodeCard>().Where(card => card.IsVisible)];

    private static List<ContentPresenter> Slots(NodeGraph graph, GraphNode node) =>
        [.. Cards(graph)
            .Where(card => ReferenceEquals(card.Node, node))
            .SelectMany(card => card.GetVisualChildren().OfType<ContentPresenter>())];

    private static NodeGraph Open(out Window window, out GraphNode withStrip, out GraphNode without)
    {
        var model = new GraphModel();

        withStrip = new GraphNode("a", "Cooked", "gen", 138)
        {
            BodyHeight = 122,
            FooterHeight = 20,
            PortLayout = PortLayout.Edge,
        };

        without = new GraphNode("b", "Plain", "gen", 138) { BodyHeight = 122 };

        withStrip.MoveTo(40, 40);
        without.MoveTo(240, 40);
        withStrip.AddOutput("Output", "Gray");
        without.AddOutput("Output", "Gray");

        model.Add(withStrip);
        model.Add(without);

        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White)),
            NodeTemplate = new FuncDataTemplate<GraphNode>((_, _) => new Border { Background = Brushes.DimGray }),
            FooterTemplate = new FuncDataTemplate<GraphNode>((_, _) => new Border { Background = Brushes.Black }),
        };

        window = new Window { Width = 600, Height = 400, Content = graph };
        window.Show();
        Settle(window);

        return graph;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
