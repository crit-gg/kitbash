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
/// The gestures, driven through real pointer events rather than through the methods behind
/// them, since the canvas hit tests the model and a captured pointer is what that is for.
/// </summary>
public class NodeGraphGestureTests
{
    [AvaloniaFact]
    public void DraggingFromAPinOntoAnotherWiresThem()
    {
        var graph = Two(out var window, out var from, out var to);

        try
        {
            var start = Screen(graph, from.Outputs[0]);
            var end = Screen(graph, to.Inputs[0]);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point((start.X + end.X) / 2, start.Y - 40));
            window.MouseMove(end);
            window.MouseUp(end, MouseButton.Left);
            Settle(window);

            Assert.Single(graph.Model!.Links);
            Assert.Same(from.Outputs[0], graph.Model.Links[0].From);
            Assert.Same(to.Inputs[0], graph.Model.Links[0].To);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APressThatWobblesLeavesTheNodeWhereItWas()
    {
        var graph = Two(out var window, out var node, out _);

        try
        {
            var at = graph.View.ToScreen(new Point(node.X + 40, node.Y + 8));
            var was = new Point(node.X, node.Y);
            var nudged = new Point(at.X + 2, at.Y + 2);

            window.MouseDown(at, MouseButton.Left);
            window.MouseMove(nudged);
            window.MouseUp(nudged, MouseButton.Left);
            Settle(window);

            Assert.Equal(was.X, node.X);
            Assert.Equal(was.Y, node.Y);
            Assert.True(graph.Selection.Contains(node));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APressThatTravelsMovesTheNode()
    {
        var graph = Two(out var window, out var node, out _);

        try
        {
            var at = graph.View.ToScreen(new Point(node.X + 40, node.Y + 8));
            var was = new Point(node.X, node.Y);
            var far = new Point(at.X + 90, at.Y + 60);

            window.MouseDown(at, MouseButton.Left);
            window.MouseMove(new Point(at.X + 30, at.Y + 20));
            window.MouseMove(far);
            window.MouseUp(far, MouseButton.Left);
            Settle(window);

            // The node keeps the point it was grabbed by, so it catches the pointer up
            // rather than trailing the reach behind it.
            Assert.Equal(was.X + 90, node.X);
            Assert.Equal(was.Y + 60, node.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AWireLetGoOverNothingAsksForANode()
    {
        var graph = Two(out var window, out var from, out _);
        var asks = new List<GraphPaletteEventArgs>();

        graph.PaletteAsked += (_, e) => asks.Add(e);

        try
        {
            var start = Screen(graph, from.Outputs[0]);
            var empty = new Point(start.X + 40, start.Y + 260);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(empty);
            window.MouseUp(empty, MouseButton.Left);
            Settle(window);

            Assert.Empty(graph.Model!.Links);

            var ask = Assert.Single(asks);

            Assert.Same(from.Outputs[0], ask.From);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APinIsNeverWiredToOneTheRulesRefuse()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Rules = new DelegatePortRules((_, _) => false);

        try
        {
            var start = Screen(graph, from.Outputs[0]);
            var end = Screen(graph, to.Inputs[0]);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end);

            Assert.Equal(PortState.Refused, graph.PortState(to.Inputs[0]));

            window.MouseUp(end, MouseButton.Left);
            Settle(window);

            Assert.Empty(graph.Model!.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PressingAWiredInputCarriesThatWireRatherThanStartingASecond()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            var end = Screen(graph, to.Inputs[0]);

            window.MouseDown(end, MouseButton.Left);
            Settle(window);

            // The wire is off the graph while it is carried, so letting go over nothing
            // leaves it off rather than leaving two.
            Assert.Empty(graph.Model.Links);

            window.MouseUp(new Point(end.X, end.Y + 240), MouseButton.Left);
            Settle(window);

            Assert.Empty(graph.Model.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingANodeMovesEverythingPicked()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Selection.Add(from);
        graph.Selection.Add(to);
        Settle(window);

        var wasFrom = new Point(from.X, from.Y);
        var wasTo = new Point(to.X, to.Y);

        try
        {
            var grab = graph.View.ToScreen(new Point(from.X + 60, from.Y + 10));

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(50, 30));
            window.MouseUp(grab + new Vector(50, 30), MouseButton.Left);
            Settle(window);

            Assert.Equal(wasFrom.X + 50, from.X, 3);
            Assert.Equal(wasFrom.Y + 30, from.Y, 3);
            Assert.Equal(wasTo.X + 50, to.X, 3);
            Assert.Equal(wasTo.Y + 30, to.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheCaretCollapsesANodeAndTheBoxFollows()
    {
        var graph = Two(out var window, out var from, out _);

        try
        {
            var tall = from.Height;
            var caret = graph.View.ToScreen(new Point(from.X + from.Width - 12, from.Y + 10));

            window.MouseDown(caret, MouseButton.Left);
            window.MouseUp(caret, MouseButton.Left);
            Settle(window);

            Assert.True(from.IsCollapsed);
            Assert.True(from.Height < tall);
            Assert.Equal(GraphMetrics.Dense.HeaderHeight, from.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ARerouteIsFoundAndCarriedByItself()
    {
        var graph = Two(out var window, out var from, out var to);
        var link = graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));

        var at = new Point(
            (from.PortPoint(from.Outputs[0]).X + to.PortPoint(to.Inputs[0]).X) / 2,
            from.PortPoint(from.Outputs[0]).Y + 40);

        link.Reroute(at);
        Settle(window);

        try
        {
            Assert.Equal(GraphHitKind.Reroute, graph.HitTest(at).Kind);

            var grab = graph.View.ToScreen(at);

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(0, 60));
            window.MouseUp(grab + new Vector(0, 60), MouseButton.Left);
            Settle(window);

            Assert.Equal(at.Y + 60, link.Reroutes[0].Y, 3);
            Assert.Contains(link, graph.Selection.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DeleteTakesTheSelectionAndEveryWireOnIt()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        graph.Selection.Set(from);
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
            Settle(window);

            Assert.Single(graph.Model.Nodes);
            Assert.Empty(graph.Model.Links);
            Assert.Equal(0, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AltAndAPressOnAWireBreaksIt()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            var start = from.PortPoint(from.Outputs[0]);
            var end = to.PortPoint(to.Inputs[0]);
            var middle = graph.View.ToScreen(new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2));

            window.MouseDown(middle, MouseButton.Left, RawInputModifiers.Alt);
            window.MouseUp(middle, MouseButton.Left, RawInputModifiers.Alt);
            Settle(window);

            Assert.Empty(graph.Model.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheWheelZoomsAboutThePointer()
    {
        var graph = Two(out var window, out _, out _);

        try
        {
            var at = new Point(500, 400);
            var under = graph.View.ToGraph(at);

            window.MouseWheel(at, new Vector(0, 1));
            Settle(window);

            Assert.True(graph.View.Zoom > 1);

            var still = graph.View.ToGraph(at);

            Assert.Equal(under.X, still.X, 2);
            Assert.Equal(under.Y, still.Y, 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, -1)]
    public void TheBandPicksTheSameNodesWhicheverWayItIsDragged(int across, int down)
    {
        var graph = Two(out var window, out var from, out var to);

        try
        {
            // A corner of the box that holds both nodes, and the opposite corner, so every
            // one of the four drags covers exactly the same ground.
            var box = new Rect(
                from.X - 20,
                from.Y - 20,
                to.X + to.Width + 20 - (from.X - 20),
                to.Y + to.Height + 20 - (from.Y - 20));

            var start = graph.View.ToScreen(new Point(
                across > 0 ? box.X : box.Right,
                down > 0 ? box.Y : box.Bottom));

            var end = graph.View.ToScreen(new Point(
                across > 0 ? box.Right : box.X,
                down > 0 ? box.Bottom : box.Y));

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2));
            window.MouseMove(end);
            window.MouseUp(end, MouseButton.Left);
            Settle(window);

            Assert.Equal(2, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EscapePutsBackWhateverIsBeingDragged()
    {
        var graph = Two(out var window, out var from, out _);
        var was = new Point(from.X, from.Y);

        graph.Selection.Set(from);
        Settle(window);

        try
        {
            var grab = graph.View.ToScreen(new Point(from.X + 60, from.Y + 10));

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(120, 90));
            Settle(window);

            Assert.NotEqual(was.X, from.X);

            graph.Cancel();
            Settle(window);

            // A drag that cannot be taken back is a drag a person is afraid to start.
            Assert.Equal(was.X, from.X, 3);
            Assert.Equal(was.Y, from.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CancellingAWireCarriedOffAnInputPutsItBack()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            var end = Screen(graph, to.Inputs[0]);

            window.MouseDown(end, MouseButton.Left);
            window.MouseMove(end + new Vector(-60, 120));
            Settle(window);

            Assert.Empty(graph.Model.Links);

            graph.Cancel();
            Settle(window);

            var link = Assert.Single(graph.Model.Links);

            Assert.Same(from.Outputs[0], link.From);
            Assert.Same(to.Inputs[0], link.To);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DuplicateCopiesTheWiresBetweenWhatIsPickedAndNoOthers()
    {
        var graph = Two(out var window, out var from, out var to);
        var outside = graph.Model!.Add(new GraphNode("c", "Other", "data", 170));

        outside.MoveTo(80, 400);
        outside.AddOutput("Out", "Float");
        to.AddInput("Second", "Float");

        graph.Model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        graph.Model.Add(new GraphLink(outside.Outputs[0], to.Inputs[1]));

        graph.Selection.Add(from);
        graph.Selection.Add(to);
        Settle(window);

        try
        {
            graph.Duplicate();
            Settle(window);

            Assert.Equal(5, graph.Model.Nodes.Count);
            Assert.Equal(2, graph.Selection.Count);

            // The wire between the two copies came along. The one reaching in from a node
            // that was not picked did not, since a copy that quietly reads someone else's
            // output is a surprise.
            Assert.Equal(3, graph.Model.Links.Count);

            var copies = graph.Selection.Nodes.ToArray();

            Assert.Contains(graph.Model.Links, link =>
                copies.Contains(link.FromNode) && copies.Contains(link.ToNode));

            Assert.DoesNotContain(graph.Model.Links, link =>
                ReferenceEquals(link.FromNode, outside) && copies.Contains(link.ToNode));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingAFrameCarriesWhateverIsStandingOnIt()
    {
        var graph = Two(out var window, out var from, out _);
        var frame = graph.Model!.Add(new GraphFrame("f", "Group", Colors.MediumPurple)
        {
            X = from.X - 20,
            Y = from.Y - 20,
            Width = from.Width + 40,
            Height = from.Height + 40,
        });

        Settle(window);

        var wasNode = new Point(from.X, from.Y);

        try
        {
            var tab = graph.View.ToScreen(new Point(frame.X + 20, frame.Y - 12));

            Assert.Equal(GraphHitKind.FrameLabel, graph.HitTest(graph.View.ToGraph(tab)).Kind);

            window.MouseDown(tab, MouseButton.Left);
            window.MouseMove(tab + new Vector(70, 40));
            window.MouseUp(tab + new Vector(70, 40), MouseButton.Left);
            Settle(window);

            // Moving the box and leaving the nodes behind would point the label at nothing.
            Assert.Equal(wasNode.X + 70, from.X, 3);
            Assert.Equal(wasNode.Y + 40, from.Y, 3);

            // The nodes came along without being picked, since the frame is what was grabbed.
            Assert.Equal(1, graph.Selection.Count);
            Assert.Contains(frame, graph.Selection.Items);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RestingOnAWireLightsItAndNothingElse()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            var start = from.PortPoint(from.Outputs[0]);
            var end = to.PortPoint(to.Inputs[0]);
            var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

            window.MouseMove(graph.View.ToScreen(middle));
            Settle(window);

            // A wire under the pointer is still a wire. Nothing is offered on it, since a
            // button appearing wherever a pointer happens to rest acts on a wire nobody
            // asked about.
            Assert.Equal(GraphHitKind.Link, graph.HitTest(middle).Kind);

            window.MouseDown(graph.View.ToScreen(middle), MouseButton.Left);
            window.MouseUp(graph.View.ToScreen(middle), MouseButton.Left);
            Settle(window);

            Assert.Single(graph.Model.Links);
            Assert.Contains(graph.Model.Links[0], graph.Selection.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ARerouteComesOffTheWayItWentOn()
    {
        var graph = Two(out var window, out var from, out var to);
        var link = graph.Model!.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));

        var at = new Point(
            (from.PortPoint(from.Outputs[0]).X + to.PortPoint(to.Inputs[0]).X) / 2,
            from.PortPoint(from.Outputs[0]).Y + 40);

        link.Reroute(at);
        Settle(window);

        try
        {
            var on = graph.View.ToScreen(at);

            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Settle(window);

            Assert.Empty(link.Reroutes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BypassTurnsThePickedNodesOffAndBackOnAgain()
    {
        var graph = Two(out var window, out var from, out var to);

        graph.Selection.Add(from);
        graph.Selection.Add(to);
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
            Settle(window);

            Assert.True(from.IsBypassed);
            Assert.True(to.IsBypassed);

            window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
            Settle(window);

            Assert.False(from.IsBypassed);
            Assert.False(to.IsBypassed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectAllTakesEverythingWithABox()
    {
        var graph = Two(out var window, out _, out _);

        graph.Model!.Add(new GraphFrame("f", "Group", Colors.MediumPurple) { Width = 200, Height = 120 });
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Settle(window);

            Assert.Equal(3, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point Screen(NodeGraph graph, GraphPort port) =>
        graph.View.ToScreen(port.Node!.PortPoint(port));

    private static NodeGraph Two(out Window window, out GraphNode from, out GraphNode to)
    {
        var model = new GraphModel();

        from = model.Add(new GraphNode("a", "Source", "data", 170));
        to = model.Add(new GraphNode("b", "Sink", "out", 170));

        from.MoveTo(80, 120);
        to.MoveTo(520, 200);
        from.AddOutput("Out", "Float");
        to.AddInput("In", "Float");

        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White)),
        };

        window = new Window { Width = 1000, Height = 700, Content = graph };
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
