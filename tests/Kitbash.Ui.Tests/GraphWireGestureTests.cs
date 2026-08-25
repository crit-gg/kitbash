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
/// The two gestures every node editor has for working on wires in bulk: dropping a node onto
/// one so it goes in, and drawing a stroke through a run of them to cut them.
/// </summary>
public class GraphWireGestureTests
{
    [AvaloniaFact]
    public void ANodeDroppedOnAWireGoesIntoIt()
    {
        var graph = Open(out var window, out var from, out var to, out var middle);
        var model = graph.Model!;

        model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            graph.Selection.Set(middle);
            Settle(window);

            Drop(graph, window, middle, Middle(from, to));

            // One wire became two, running through the node that was dropped on it.
            Assert.Equal(2, model.Links.Count);
            Assert.Same(middle.Inputs[0], model.LinkInto(middle.Inputs[0])?.To);
            Assert.Same(from.Outputs[0], model.LinkInto(middle.Inputs[0])?.From);
            Assert.Same(middle.Outputs[0], model.LinkInto(to.Inputs[0])?.From);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ANodeThatCannotTakeTheWireIsLeftWhereItLands()
    {
        var graph = Open(out var window, out var from, out var to, out var middle);
        var model = graph.Model!;

        model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));

        // Nothing on it the wire could land on, so it is a node lying over a wire and no more.
        var stranger = model.Add(new GraphNode("x", "Sink only", "out", 170));

        // Clear of the node the helper already parked down there, or the press would land on
        // whichever of the two the index answered with first.
        stranger.MoveTo(620, 620);
        stranger.AddInput("In", "Float");
        Settle(window);

        try
        {
            graph.Selection.Set(stranger);
            Settle(window);

            Drop(graph, window, stranger, Middle(from, to));

            Assert.Single(model.Links);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ASpliceKeepsTheRunTheWireWasRoutedThrough()
    {
        var graph = Open(out var window, out var from, out var to, out var middle);
        var model = graph.Model!;
        var link = model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));

        link.Reroute(new Point(300, 60));
        middle.MoveTo(340, 200);
        Settle(window);

        try
        {
            Assert.True(graph.Splice(middle, link));

            // The points belong to the length before the node, since that is the length they
            // were on. The new length after it starts clean.
            Assert.Equal(2, model.Links.Count);
            Assert.Single(model.LinkInto(middle.Inputs[0])!.Reroutes);
            Assert.Empty(model.LinkInto(to.Inputs[0])!.Reroutes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AStrokeTakesOffEveryWireItCrosses()
    {
        var graph = Open(out var window, out var from, out var to, out _);
        var model = graph.Model!;

        // Three wires side by side, and a stroke drawn across all of them.
        for (var step = 0; step < 3; step++)
        {
            var one = model.Add(new GraphNode($"a{step}", "From", "data", 120));
            var two = model.Add(new GraphNode($"b{step}", "To", "out", 120));

            one.MoveTo(60, 60 + step * 90);
            two.MoveTo(420, 60 + step * 90);
            one.AddOutput("Out", "Float");
            two.AddInput("In", "Float");
            model.Add(new GraphLink(one.Outputs[0], two.Inputs[0]));
        }

        Settle(window);

        var before = model.Links.Count;

        try
        {
            var cut = graph.Cut(new Point(300, 40), new Point(300, 300));

            Assert.Equal(3, cut);
            Assert.Equal(before - 3, model.Links.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ControlAndADragDrawsTheStrokeAndCutsWithIt()
    {
        var graph = Open(out var window, out var from, out var to, out _);
        var model = graph.Model!;

        model.Add(new GraphLink(from.Outputs[0], to.Inputs[0]));
        Settle(window);

        try
        {
            var above = graph.View.ToScreen(new Point(360, 40));
            var below = graph.View.ToScreen(new Point(360, 260));

            window.MouseDown(above, MouseButton.Left, RawInputModifiers.Control);
            window.MouseMove(below);
            Settle(window);

            // The stroke is drawn while it is held, so a person can see what it will take.
            Assert.NotNull(graph.CutStroke);

            window.MouseUp(below, MouseButton.Left, RawInputModifiers.Control);
            Settle(window);

            Assert.Null(graph.CutStroke);
            Assert.Empty(model.Links);

            // And it picked nothing, since a cut is not a box select.
            Assert.Equal(0, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Halfway along the wire between two nodes, in graph units.</summary>
    private static Point Middle(GraphNode from, GraphNode to)
    {
        var start = from.PortPoint(from.Outputs[0]);
        var end = to.PortPoint(to.Inputs[0]);

        return new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
    }

    /// <summary>Drags a node by its header until its middle sits on a point.</summary>
    private static void Drop(NodeGraph graph, Window window, GraphNode node, Point onto)
    {
        var hold = new Point(node.X + node.Width / 2, node.Y + 10);
        var by = onto - node.Bounds.Center;

        var grab = graph.View.ToScreen(hold);
        var let = graph.View.ToScreen(hold + by);

        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(new Point((grab.X + let.X) / 2, (grab.Y + let.Y) / 2));
        window.MouseMove(let);
        window.MouseUp(let, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static NodeGraph Open(out Window window, out GraphNode from, out GraphNode to, out GraphNode middle)
    {
        var model = new GraphModel();

        from = model.Add(new GraphNode("from", "Source", "data", 170));
        to = model.Add(new GraphNode("to", "Sink", "out", 170));
        middle = model.Add(new GraphNode("mid", "Filter", "math", 170));

        from.MoveTo(60, 60);
        to.MoveTo(620, 100);
        middle.MoveTo(300, 600);

        from.AddOutput("Out", "Float");
        to.AddInput("In", "Float");
        middle.AddInput("In", "Float");
        middle.AddOutput("Out", "Float");

        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White)),
        };

        window = new Window { Width = 1000, Height = 800, Content = graph };
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
