using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The node graph. What matters here is the rule the whole control is built on: the geometry
/// is data, only what meets the viewport is realised, and hit testing reads the model.
/// </summary>
public class NodeGraphTests
{
    private const int Wide = 1200;
    private const int Tall = 800;

    [AvaloniaFact]
    public void ANodesBoxComesFromItsPortsAndNoControlAtAll()
    {
        var model = new GraphModel();
        var node = model.Add(new GraphNode("n1", "Add", "math", 168));

        node.AddInput("A", "Float", "0");
        node.AddInput("B", "Float", "0");
        node.AddOutput("Out", "Float");

        var metrics = GraphMetrics.Dense;

        Assert.Equal(metrics.HeaderHeight + metrics.BodyPadding * 2 + metrics.RowHeight * 2, node.Height);

        // The pin sits in the middle of its own row, which is the one formula a wire and a
        // card both read.
        var pin = node.PortPoint(node.Inputs[1]);

        Assert.Equal(node.X, pin.X);
        Assert.Equal(node.Y + metrics.HeaderHeight + metrics.BodyPadding + metrics.RowHeight * 1.5, pin.Y);

        node.IsCollapsed = true;

        Assert.Equal(metrics.HeaderHeight, node.Height);
    }

    [AvaloniaFact]
    public void OnlyWhatMeetsTheViewportIsRealised()
    {
        var graph = Open(Grid(24, 24), out var window);
        var panel = Panel(graph);

        try
        {
            // Six hundred nodes on a grid far wider than the window. Nothing off screen may
            // hold a container, whatever the graph holds in all.
            Assert.Equal(576, graph.Model!.Nodes.Count);

            graph.View.Zoom = 1;
            graph.View.Offset = default;
            Settle(window);

            var atRest = panel.RealisedCount;

            Assert.InRange(atRest, 1, 60);

            // Pan right across the whole graph. The realised count is what must stay bounded,
            // not the graph's size.
            var most = atRest;

            for (var step = 1; step <= 40; step++)
            {
                graph.View.Offset = new Vector(-step * 400, -step * 120);
                Settle(window);
                most = Math.Max(most, panel.RealisedCount);
            }

            Assert.InRange(most, 1, 80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ZoomedOutFarEnoughNothingIsRealisedAtAll()
    {
        var graph = Open(Grid(24, 24), out var window);
        var panel = Panel(graph);

        try
        {
            graph.View.Zoom = 1;
            Settle(window);

            Assert.True(panel.RealisedCount > 0);

            graph.View.Zoom = 0.15;
            Settle(window);

            Assert.Equal(GraphLod.Block, graph.View.Lod);
            Assert.Equal(0, panel.RealisedCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FitPutsTheWholeGraphInView()
    {
        var graph = Open(Grid(6, 6), out var window);

        try
        {
            graph.View.Zoom = 2;
            graph.View.Offset = new Vector(4000, 4000);
            Settle(window);

            graph.Fit();
            Settle(window);

            var seen = graph.View.Viewport(graph.Bounds.Size);
            var content = graph.Model!.ContentBounds;

            Assert.True(seen.Contains(content), $"{content} is not inside {seen}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HitTestingAnswersPinsBeforeNodes()
    {
        var model = new GraphModel();
        var node = model.Add(new GraphNode("n1", "Add", "math", 168));

        node.MoveTo(100, 100);
        node.AddInput("A", "Float", "0");
        node.AddOutput("Out", "Float");

        var graph = Open(model, out var window);

        try
        {
            var pin = node.PortPoint(node.Inputs[0]);

            Assert.Equal(GraphHitKind.Port, graph.HitTest(pin).Kind);

            // A point well inside the body is the node, and the header is told apart from it.
            Assert.Equal(GraphHitKind.Node, graph.HitTest(new Point(node.X + 80, node.Y + 44)).Kind);
            Assert.Equal(GraphHitKind.NodeHeader, graph.HitTest(new Point(node.X + 60, node.Y + 8)).Kind);
            Assert.Equal(GraphHitKind.NodeCaret, graph.HitTest(new Point(node.X + 160, node.Y + 8)).Kind);
            Assert.Equal(GraphHitKind.None, graph.HitTest(new Point(600, 600)).Kind);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AWireIsFoundAlongItsPathAndNowhereElse()
    {
        var model = new GraphModel();
        var from = model.Add(new GraphNode("a", "Source", "data", 160));
        var to = model.Add(new GraphNode("b", "Sink", "out", 160));

        from.MoveTo(0, 0);
        to.MoveTo(400, 0);

        var output = from.AddOutput("Out", "Float");
        var input = to.AddInput("In", "Float");

        model.Add(new GraphLink(output, input));

        var graph = Open(model, out var window);

        try
        {
            var start = from.PortPoint(output);
            var end = to.PortPoint(input);
            var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

            Assert.Equal(GraphHitKind.Link, graph.HitTest(middle).Kind);

            // Off the wire by more than its reach and nothing is under the point.
            Assert.Equal(GraphHitKind.None, graph.HitTest(middle + new Vector(0, 40)).Kind);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AnInputHoldsOneWireAndTheSecondReplacesTheFirst()
    {
        var model = new GraphModel();
        var one = model.Add(new GraphNode("a", "One", "data", 160));
        var two = model.Add(new GraphNode("b", "Two", "data", 160));
        var sink = model.Add(new GraphNode("c", "Sink", "out", 160));

        var first = one.AddOutput("Out", "Float");
        var second = two.AddOutput("Out", "Float");
        var input = sink.AddInput("In", "Float");

        model.Add(new GraphLink(first, input));
        model.Add(new GraphLink(second, input));

        Assert.Single(model.Links);
        Assert.Same(second, model.LinkInto(input)!.From);
    }

    [AvaloniaFact]
    public void TheRulesRefuseAWireTheGraphShouldNotHave()
    {
        var model = new GraphModel();
        var one = model.Add(new GraphNode("a", "One", "data", 160));
        var two = model.Add(new GraphNode("b", "Two", "data", 160));

        var number = one.AddOutput("Out", "Float");
        var flag = two.AddInput("In", "Bool");
        var value = two.AddInput("Value", "Float");

        Assert.Null(model.TryJoin(number, flag, PortRules.Strict));
        Assert.NotNull(model.TryJoin(number, value, PortRules.Strict));

        // A port never joins its own node, whatever the rules say.
        Assert.Null(model.TryJoin(number, one.AddInput("Back", "Float"), PortRules.Strict));
    }

    [AvaloniaFact]
    public void MovingANodeMovesEveryWireOnIt()
    {
        var model = new GraphModel();
        var from = model.Add(new GraphNode("a", "Source", "data", 160));
        var to = model.Add(new GraphNode("b", "Sink", "out", 160));

        to.MoveTo(400, 0);

        var link = model.Add(new GraphLink(from.AddOutput("Out", "Float"), to.AddInput("In", "Float")));
        var graph = Open(model, out var window);

        try
        {
            Settle(window);

            var middle = Halfway(from, to);

            Assert.Equal(GraphHitKind.Link, graph.HitTest(middle).Kind);

            to.MoveTo(400, 300);
            Settle(window);

            // The wire left where it was, which is the cached route being thrown away rather
            // than a stale path still answering for it.
            Assert.Equal(GraphHitKind.None, graph.HitTest(middle).Kind);
            Assert.Equal(GraphHitKind.Link, graph.HitTest(Halfway(from, to)).Kind);
            Assert.Same(link, model.Links[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingOnEmptySpacePicksEveryNodeTheBandCovers()
    {
        var graph = Open(Grid(4, 4), out var window);

        try
        {
            graph.View.Zoom = 1;
            graph.View.Offset = new Vector(80, 80);
            Settle(window);

            var first = graph.Model!.Nodes[0];
            var corner = graph.View.ToScreen(new Point(first.X - 30, first.Y - 30));

            window.MouseDown(corner, MouseButton.Left);
            window.MouseMove(corner + new Vector(360, 300));
            window.MouseUp(corner + new Vector(360, 300), MouseButton.Left);
            Settle(window);

            Assert.True(graph.Selection.Count > 0, "the band picked nothing");
            Assert.All(graph.Selection.Items, item => Assert.True(item.IsSelected));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheCanvasDrawsSomethingOtherThanItsGround()
    {
        var graph = Open(Grid(3, 3), out var window);

        try
        {
            graph.Fit();
            Settle(window);

            var frame = window.CaptureRenderedFrame()!;

            using var buffer = frame.Lock();

            var bytes = new byte[buffer.RowBytes * buffer.Size.Height];

            Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            var seen = new HashSet<int>();

            for (var y = 0; y < buffer.Size.Height; y += 3)
            {
                for (var x = 0; x < buffer.Size.Width; x += 3)
                {
                    seen.Add(BitConverter.ToInt32(bytes, y * buffer.RowBytes + x * 4));
                }
            }

            // The ground, the grid, the node fills, the header, the pins and the wires. A
            // canvas that drew nothing would come back with one colour on it.
            Assert.True(seen.Count > 6, $"only {seen.Count} colours were drawn");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AWireRunningBackwardsIsStillFoundAndStillDrawn()
    {
        var model = new GraphModel();
        var from = model.Add(new GraphNode("a", "Source", "data", 160));
        var to = model.Add(new GraphNode("b", "Sink", "out", 160));

        // The output sits to the right of the input, which is what a graph that loops back
        // looks like and what a node dragged past its target looks like.
        from.MoveTo(400, 0);
        to.MoveTo(0, 120);

        var link = model.Add(new GraphLink(from.AddOutput("Out", "Float"), to.AddInput("In", "Float")));
        var graph = Open(model, out var window);

        try
        {
            Settle(window);

            Assert.Equal(GraphHitKind.Link, graph.HitTest(Halfway(from, to)).Kind);
            Assert.Contains(link, Reached(model, graph));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every wire the index answers with over the whole graph.</summary>
    private static List<GraphLink> Reached(GraphModel model, NodeGraph graph)
    {
        var found = new List<GraphLink>();

        model.QueryLinks(model.ContentBounds.Inflate(200), found);

        return found;
    }

    /// <summary>The point halfway along a wire between two nodes' first ports.</summary>
    private static Point Halfway(GraphNode from, GraphNode to)
    {
        var start = from.PortPoint(from.Outputs[0]);
        var end = to.PortPoint(to.Inputs[0]);

        return new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
    }

    [AvaloniaFact]
    public void TheComfortableIncludeSwapsTheWholeMetricsObject()
    {
        var app = Avalonia.Application.Current!;
        var graph = Open(Grid(2, 2), out var window);

        try
        {
            Assert.Same(GraphMetrics.Dense, graph.Metrics);

            var node = graph.Model!.Nodes[0];
            var dense = node.Height;

            var comfortable = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Kitbash.Ui/"))
            {
                Source = new Uri("avares://Kitbash.Ui/Themes/KitbashComfortable.axaml"),
            };

            app.Styles.Add(comfortable);

            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.Same(GraphMetrics.Comfortable, graph.Metrics);

                // The model is rebuilt with it, so every node's box moved without being told.
                Assert.True(node.Height > dense, $"{node.Height} is not taller than {dense}");
            }
            finally
            {
                app.Styles.Remove(comfortable);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static GraphModel Grid(int across, int down)
    {
        var model = new GraphModel();

        for (var y = 0; y < down; y++)
        {
            for (var x = 0; x < across; x++)
            {
                var node = new GraphNode($"n{y}_{x}", $"Node {y}.{x}", "math", 168);

                node.MoveTo(x * 260, y * 180);
                node.AddInput("A", "Float", "0");
                node.AddInput("B", "Float", "1");
                node.AddOutput("Out", "Float");

                model.Add(node);

                if (x > 0)
                {
                    model.Add(new GraphLink(
                        model.Nodes[^2].Outputs[0],
                        node.Inputs[0]));
                }
            }
        }

        return model;
    }

    private static NodeGraphPanel Panel(NodeGraph graph) =>
        graph.GetVisualDescendants().OfType<NodeGraphPanel>().First();

    private static NodeGraph Open(GraphModel model, out Window window)
    {
        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White))
                .Add("math", IconGlyph.Shapes, Brushes.CornflowerBlue)
                .Add("data", IconGlyph.Database, Brushes.MediumAquamarine)
                .Add("out", IconGlyph.ArrowToBottom, Brushes.LightSkyBlue),
        };

        window = new Window { Width = Wide, Height = Tall, Content = graph };
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
