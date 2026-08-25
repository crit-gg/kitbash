using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A wire takes as many reroute points as a person adds, in the order it passes them, so a
/// long one can be routed round whatever is in its way.
/// </summary>
public class NodeWireTests
{
    [AvaloniaFact]
    public void AWirePassesThroughEveryPointItIsDraggedThrough()
    {
        var graph = Open(out var window, out var link);

        try
        {
            var run = new[] { new Point(320, 320), new Point(400, 120), new Point(470, 300) };

            foreach (var at in run)
            {
                link.Reroute(at);
            }

            Settle(window);

            Assert.Equal(3, link.Reroutes.Count);

            foreach (var at in run)
            {
                Assert.Equal(GraphHitKind.Reroute, graph.HitTest(at).Kind);

                // Not just near it. The wire really goes through the point, which the router
                // is asked directly rather than through anything the canvas cached.
                var path = WireRouter.Build(
                    link.FromNode.PortPoint(link.From),
                    link.ToNode.PortPoint(link.To),
                    link.Reroutes,
                    WireStyle.Bezier,
                    GraphMetrics.Dense);

                Assert.True(path.StrokeContains(new Pen(Brushes.Black, 2), at), $"the wire misses {at}");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APointGoesOnTheLengthOfWireItWasAddedTo()
    {
        var graph = Open(out var window, out var link);

        try
        {
            var middle = new Point(400, 260);

            link.Reroute(middle);
            Settle(window);

            // A wire with one point has two lengths. A press on the first has to come before
            // the point already there, or the wire doubles back on itself.
            var from = link.FromNode.PortPoint(link.From);
            var to = link.ToNode.PortPoint(link.To);

            Assert.Equal(0, graph.Length(link, Halfway(from, middle)));
            Assert.Equal(1, graph.Length(link, Halfway(middle, to)));

            link.Reroute(graph.Length(link, Halfway(from, middle)), new Point(280, 240));

            Assert.Equal(280, link.Reroutes[0].X, 3);
            Assert.Equal(400, link.Reroutes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingOnePointLeavesTheOthersWhereTheyWere()
    {
        var graph = Open(out var window, out var link);

        link.Reroute(new Point(320, 300));
        link.Reroute(new Point(460, 300));
        Settle(window);

        try
        {
            var grab = graph.View.ToScreen(link.Reroutes[1]);

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(0, 70));
            window.MouseUp(grab + new Vector(0, 70), MouseButton.Left);
            Settle(window);

            Assert.Equal(300, link.Reroutes[0].Y, 3);
            Assert.Equal(370, link.Reroutes[1].Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EscapePutsOneMovedPointBackAndOnlyThatOne()
    {
        var graph = Open(out var window, out var link);

        link.Reroute(new Point(320, 300));
        link.Reroute(new Point(460, 300));
        Settle(window);

        try
        {
            var grab = graph.View.ToScreen(link.Reroutes[0]);

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(40, 90));
            Settle(window);

            Assert.NotEqual(300, link.Reroutes[0].Y);

            graph.Cancel();
            Settle(window);

            Assert.Equal(320, link.Reroutes[0].X, 3);
            Assert.Equal(300, link.Reroutes[0].Y, 3);
            Assert.Equal(460, link.Reroutes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADoubleClickTakesOffOnlyThePointItLandedOn()
    {
        var graph = Open(out var window, out var link);

        link.Reroute(new Point(320, 300));
        link.Reroute(new Point(460, 300));
        Settle(window);

        try
        {
            var on = graph.View.ToScreen(link.Reroutes[0]);

            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Settle(window);

            var left = Assert.Single(link.Reroutes);

            Assert.Equal(460, left.X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADoubleClickOnTheWireAddsOneWhereItLanded()
    {
        var graph = Open(out var window, out var link);

        try
        {
            var from = link.FromNode.PortPoint(link.From);
            var to = link.ToNode.PortPoint(link.To);
            var middle = Halfway(from, to);

            window.MouseDown(graph.View.ToScreen(middle), MouseButton.Left);
            window.MouseUp(graph.View.ToScreen(middle), MouseButton.Left);
            window.MouseDown(graph.View.ToScreen(middle), MouseButton.Left);
            window.MouseUp(graph.View.ToScreen(middle), MouseButton.Left);
            Settle(window);

            var added = Assert.Single(link.Reroutes);

            Assert.Equal(middle.X, added.X, 1);
            Assert.Equal(middle.Y, added.Y, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NothingIsOfferedOnAWireUnderThePointer()
    {
        var graph = Open(out var window, out var link);

        try
        {
            var middle = Halfway(link.FromNode.PortPoint(link.From), link.ToNode.PortPoint(link.To));

            window.MouseMove(graph.View.ToScreen(middle));
            Settle(window);

            // A wire is a wire whether or not a pointer is resting on it. Alt and a click is
            // what takes one off, and it is the only thing that does.
            Assert.Equal(GraphHitKind.Link, graph.HitTest(middle).Kind);

            window.MouseDown(graph.View.ToScreen(middle), MouseButton.Left, RawInputModifiers.Alt);
            window.MouseUp(graph.View.ToScreen(middle), MouseButton.Left, RawInputModifiers.Alt);
            Settle(window);

            Assert.Empty(graph.Model!.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ACarriedWirePutBackKeepsEveryPointItHad()
    {
        var graph = Open(out var window, out var link);

        link.Reroute(new Point(320, 300));
        link.Reroute(new Point(460, 300));
        Settle(window);

        try
        {
            var end = graph.View.ToScreen(link.ToNode.PortPoint(link.To));

            window.MouseDown(end, MouseButton.Left);
            window.MouseMove(end + new Vector(-40, 160));
            Settle(window);

            Assert.Empty(graph.Model!.Links);

            graph.Cancel();
            Settle(window);

            var back = Assert.Single(graph.Model.Links);

            Assert.Equal(2, back.Reroutes.Count);
            Assert.Equal(320, back.Reroutes[0].X, 3);
            Assert.Equal(460, back.Reroutes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point Halfway(Point from, Point to) => new((from.X + to.X) / 2, (from.Y + to.Y) / 2);

    private static NodeGraph Open(out Window window, out GraphLink link)
    {
        var model = new GraphModel();
        var from = model.Add(new GraphNode("a", "Source", "data", 170));
        var to = model.Add(new GraphNode("b", "Sink", "out", 170));

        from.MoveTo(80, 120);
        to.MoveTo(560, 160);
        from.AddOutput("Out", "Float");
        to.AddInput("In", "Float");

        link = model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));

        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White)),
        };

        window = new Window { Width = 900, Height = 600, Content = graph };
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
