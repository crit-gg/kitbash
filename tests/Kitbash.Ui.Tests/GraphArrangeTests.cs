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
/// Tidying a graph: lining nodes up, spacing them, straightening the wires between them, and
/// reaching the run a node feeds. What every node editor offers and what a person reaches for
/// the moment a graph stops fitting on one screen.
/// </summary>
public class GraphArrangeTests
{
    [AvaloniaFact]
    public void EverythingLinesUpWithTheLastOnePicked()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(40, 40);
            nodes[1].MoveTo(300, 90);
            nodes[2].MoveTo(560, 150);

            // Picked in order, so the third is the anchor. Selection order is what says which
            // node the others move to, since that is the one just clicked.
            foreach (var node in nodes)
            {
                graph.Selection.Add(node);
            }

            Assert.Same(nodes[2], graph.Selection.Anchor);

            graph.AlignSelection(GraphEdges.Top);

            Assert.Equal(150, nodes[0].Y, 3);
            Assert.Equal(150, nodes[1].Y, 3);
            Assert.Equal(150, nodes[2].Y, 3);

            // Only the way asked for. Aligning tops must not shuffle anything sideways.
            Assert.Equal(40, nodes[0].X, 3);
            Assert.Equal(300, nodes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SpreadingLeavesTheEndsWhereTheyAreAndEvensTheGaps()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(0, 0);
            nodes[1].MoveTo(60, 0);
            nodes[2].MoveTo(700, 0);

            foreach (var node in nodes)
            {
                graph.Selection.Add(node);
            }

            graph.SpreadSelection(across: true);

            Assert.Equal(0, nodes[0].X, 3);
            Assert.Equal(700, nodes[2].X, 3);

            var first = nodes[1].X - (nodes[0].X + nodes[0].Width);
            var second = nodes[2].X - (nodes[1].X + nodes[1].Width);

            Assert.Equal(first, second, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StraighteningPutsAWireLevelWithThePinItLeaves()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(40, 40);
            nodes[1].MoveTo(320, 260);

            var link = graph.Model!.Add(new GraphLink(nodes[0].Outputs[0], nodes[1].Inputs[0]));

            graph.Selection.Add(nodes[0]);
            graph.Selection.Add(nodes[1]);
            graph.StraightenSelection();

            var from = link.FromNode.PortPoint(link.From);
            var to = link.ToNode.PortPoint(link.To);

            Assert.Equal(from.Y, to.Y, 3);

            // The node the wire goes into is the one that moves, so a chain straightens out
            // along the way it flows rather than dragging its source about.
            Assert.Equal(40, nodes[0].Y, 3);
            Assert.Equal(320, nodes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectingLinkedFollowsTheWiresAndStopsAtALoop()
    {
        var graph = Open(out var window, out var nodes);
        var model = graph.Model!;

        model.Add(new GraphLink(nodes[0].Outputs[0], nodes[1].Inputs[0]));
        model.Add(new GraphLink(nodes[1].Outputs[0], nodes[2].Inputs[0]));

        // Back to the start, which is what would walk for ever without a guard.
        model.Add(new GraphLink(nodes[2].Outputs[0], nodes[0].Inputs[0]));

        try
        {
            graph.Selection.Set(nodes[0]);
            graph.SelectLinked();

            Assert.Equal(3, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FitPutsWhatIsPickedInViewRatherThanTheWholeGraph()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(0, 0);
            nodes[1].MoveTo(400, 0);
            nodes[2].MoveTo(6000, 4000);

            graph.Selection.Set(nodes[2]);
            graph.FitSelection();
            Settle(window);

            var seen = graph.View.Viewport(graph.Bounds.Size);

            Assert.True(seen.Contains(nodes[2].Bounds), $"{nodes[2].Bounds} is not inside {seen}");
            Assert.False(seen.Contains(nodes[0].Bounds), "the whole graph was fitted instead");

            // Nothing picked falls back to the whole graph, which is what F meant before.
            graph.Selection.Clear();
            graph.FitSelection();
            Settle(window);

            Assert.True(graph.View.Viewport(graph.Bounds.Size).Contains(graph.Model!.ContentBounds));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheArrowsWalkFromNodeToNodeInThatDirection()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(0, 0);
            nodes[1].MoveTo(400, 0);
            nodes[2].MoveTo(0, 400);

            graph.Selection.Set(nodes[0]);
            graph.Focus();

            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Settle(window);

            Assert.Same(nodes[1], graph.Selection.Anchor);

            window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
            Settle(window);

            Assert.Same(nodes[0], graph.Selection.Anchor);

            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Settle(window);

            // Straight ahead beats near but off to the side, which is what the penalty is for.
            Assert.Same(nodes[2], graph.Selection.Anchor);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShiftAndAnArrowTakesTheNextOneAsWellAndControlNudges()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            nodes[0].MoveTo(0, 0);
            nodes[1].MoveTo(400, 0);

            graph.Selection.Set(nodes[0]);
            graph.Focus();

            window.KeyPress(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, null);
            Settle(window);

            Assert.Equal(2, graph.Selection.Count);

            window.KeyPress(Key.Right, RawInputModifiers.Control, PhysicalKey.ArrowRight, null);
            Settle(window);

            var step = GraphMetrics.Dense.SnapStep;

            Assert.Equal(step, nodes[0].X, 3);
            Assert.Equal(400 + step, nodes[1].X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheCanvasAndItsCardsSayWhatTheyAreHolding()
    {
        var graph = Open(out var window, out var nodes);

        try
        {
            graph.Selection.Set(nodes[1]);
            Settle(window);

            var peer = new NodeGraphAutomationPeer(graph);

            Assert.Equal("Node graph", peer.GetName());
            Assert.Contains("3 nodes", peer.GetHelpText());
            Assert.Contains("Node 1", peer.GetHelpText());

            var card = graph.GetVisualDescendants().OfType<NodeCard>().First(c => ReferenceEquals(c.Node, nodes[0]));
            var one = new NodeCardAutomationPeer(card);

            Assert.Equal("Node 0", one.GetName());
            Assert.Contains("1 in, 1 out", one.GetHelpText());

            nodes[0].IsBypassed = true;

            Assert.Contains("bypassed", one.GetHelpText());
        }
        finally
        {
            window.Close();
        }
    }

    private static NodeGraph Open(out Window window, out GraphNode[] nodes)
    {
        var model = new GraphModel();
        var made = new List<GraphNode>();

        for (var step = 0; step < 3; step++)
        {
            var node = new GraphNode($"n{step}", $"Node {step}", "math", 170);

            node.MoveTo(step * 240, 0);
            node.AddInput("In", "Float");
            node.AddOutput("Out", "Float");
            made.Add(model.Add(node));
        }

        nodes = [.. made];

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
