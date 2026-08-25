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
/// Frames, which are the groups a person draws around a run of nodes. They resize by their
/// edges, rename by their tab, carry what stands on them, and never own anything.
/// </summary>
public class GraphFrameTests
{
    [AvaloniaTheory]
    [InlineData(GraphEdges.Right, 60, 0, 0, 0, 60, 0)]
    [InlineData(GraphEdges.Bottom, 0, 40, 0, 0, 0, 40)]
    [InlineData(GraphEdges.Left, -30, 0, -30, 0, 30, 0)]
    [InlineData(GraphEdges.Top, 0, -25, 0, -25, 0, 25)]
    [InlineData(GraphEdges.Left | GraphEdges.Top, -20, -20, -20, -20, 20, 20)]
    [InlineData(GraphEdges.Right | GraphEdges.Bottom, 35, 45, 0, 0, 35, 45)]
    public void EveryEdgeAndCornerPullsTheRightWay(
        GraphEdges edges,
        double byX,
        double byY,
        double movedX,
        double movedY,
        double grewWide,
        double grewTall)
    {
        var graph = Open(out var window, out var frame, out _);
        var was = frame.Bounds;

        try
        {
            var grab = graph.View.ToScreen(Grip(frame, edges));

            Assert.Equal(edges, graph.HitTest(graph.View.ToGraph(grab)).Edge);

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(byX, byY));
            window.MouseUp(grab + new Vector(byX, byY), MouseButton.Left);
            Settle(window);

            Assert.Equal(was.X + movedX, frame.X, 3);
            Assert.Equal(was.Y + movedY, frame.Y, 3);
            Assert.Equal(was.Width + grewWide, frame.Width, 3);
            Assert.Equal(was.Height + grewTall, frame.Height, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AFrameNeverTurnsInsideOut()
    {
        var graph = Open(out var window, out var frame, out _);
        var was = frame.Bounds;

        try
        {
            var grab = graph.View.ToScreen(Grip(frame, GraphEdges.Left));

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(9000, 0));
            window.MouseUp(grab + new Vector(9000, 0), MouseButton.Left);
            Settle(window);

            Assert.Equal(GraphMetrics.Dense.FrameSmallest, frame.Width, 3);

            // The edge being dragged is the one that stops, so the opposite one stays put.
            Assert.Equal(was.Right, frame.Bounds.Right, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResizingDoesNotDragWhatIsStandingOnIt()
    {
        var graph = Open(out var window, out var frame, out var node);
        var was = new Point(node.X, node.Y);

        try
        {
            var grab = graph.View.ToScreen(Grip(frame, GraphEdges.Right));

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(80, 0));
            window.MouseUp(grab + new Vector(80, 0), MouseButton.Left);
            Settle(window);

            Assert.Equal(was.X, node.X, 3);
            Assert.Equal(was.Y, node.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EscapePutsAResizeBack()
    {
        var graph = Open(out var window, out var frame, out _);
        var was = frame.Bounds;

        try
        {
            var grab = graph.View.ToScreen(Grip(frame, GraphEdges.Right));

            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(120, 0));
            Settle(window);

            Assert.NotEqual(was.Width, frame.Width);

            graph.Cancel();
            Settle(window);

            Assert.Equal(was.Width, frame.Width, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADoubleClickOnTheTabRenamesIt()
    {
        var graph = Open(out var window, out var frame, out _);

        try
        {
            var tab = graph.View.ToScreen(frame.LabelBox(GraphMetrics.Dense).Center);

            window.MouseDown(tab, MouseButton.Left);
            window.MouseUp(tab, MouseButton.Left);
            window.MouseDown(tab, MouseButton.Left);
            window.MouseUp(tab, MouseButton.Left);
            Settle(window);

            Assert.Same(frame, graph.Renaming);

            var field = Field(graph);

            Assert.Equal("Group", field.Text);

            field.Text = "Tier scaling";
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\\r");
            Settle(window);

            Assert.Equal("Tier scaling", frame.Label);
            Assert.Null(graph.Renaming);
            Assert.False(field.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EscapeInTheRenameFieldPutsTheOldNameBack()
    {
        var graph = Open(out var window, out var frame, out _);

        try
        {
            graph.BeginRename(frame);
            Settle(window);

            var field = Field(graph);

            field.Text = "something else";
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Settle(window);

            Assert.Equal("Group", frame.Label);
            Assert.Null(graph.Renaming);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AnEmptyNameIsRefusedAndTheOldOneStands()
    {
        var graph = Open(out var window, out var frame, out _);

        try
        {
            graph.BeginRename(frame);
            Settle(window);

            Field(graph).Text = "   ";
            graph.CommitRename();
            Settle(window);

            Assert.Equal("Group", frame.Label);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void F2RenamesWhateverIsPicked()
    {
        var graph = Open(out var window, out _, out var node);

        graph.Selection.Set(node);
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null);
            Settle(window);

            Assert.Same(node, graph.Renaming);

            Field(graph).Text = "Renamed";
            graph.CommitRename();
            Settle(window);

            // A node's label is its title, so one field renames a node, a frame and a note.
            Assert.Equal("Renamed", node.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ANoteEditsAsProseOverSeveralLines()
    {
        var graph = Open(out var window, out _, out _);
        var note = graph.Model!.Add(new GraphNote("c", "First line", "Rowan")
        {
            X = 700, Y = 400, Width = 220, Height = 90,
        });

        Settle(window);

        try
        {
            graph.BeginRename(note);
            Settle(window);

            var field = Field(graph);

            Assert.True(field.AcceptsReturn);

            field.Text = "First line\\nSecond line";
            graph.CommitRename();
            Settle(window);

            Assert.Equal("First line\\nSecond line", note.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GroupingPutsAFrameRoundTheSelectionWithRoomForItsTab()
    {
        var graph = Open(out var window, out _, out var node);

        graph.Selection.Set(node);
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.G, RawInputModifiers.Control, PhysicalKey.G, "g");
            Settle(window);

            Assert.Equal(2, graph.Model!.Frames.Count);

            var made = graph.Model.Frames[^1];

            Assert.Contains(made, graph.Selection.Items);
            Assert.True(made.Bounds.Contains(node.Bounds), $"{made.Bounds} does not hold {node.Bounds}");

            // The tab is drawn above the box, so a frame made round something has to leave
            // room for it or the label lands on what it was drawn around.
            var tab = made.LabelBox(GraphMetrics.Dense);

            Assert.True(tab.Bottom <= node.Y, $"the tab at {tab} lands on the node at {node.Bounds}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TwoFramesMadeOneAfterAnotherAreNotTheSameColour()
    {
        var graph = Open(out var window, out _, out var node);

        graph.Selection.Set(node);
        Settle(window);

        try
        {
            var first = graph.FrameSelection();

            graph.Selection.Set(node);

            var second = graph.FrameSelection();

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first!.Colour, second!.Colour);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RemovingAFrameLeavesWhatStoodOnIt()
    {
        var graph = Open(out var window, out var frame, out var node);

        graph.Selection.Set(frame);
        Settle(window);

        try
        {
            graph.Focus();
            window.KeyPress(Key.G, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.G, "g");
            Settle(window);

            Assert.Empty(graph.Model!.Frames);
            Assert.Contains(node, graph.Model.Nodes);

            // Delete says the same thing, since a group is a box behind a run of nodes and
            // taking the box away has never meant taking the nodes away.
            graph.Model.Add(new GraphFrame("f2", "Again", Colors.MediumPurple)
            {
                X = 0, Y = 0, Width = 400, Height = 300,
            });

            graph.Selection.Set(graph.Model.Frames[0]);
            graph.DeleteSelection();

            Assert.Empty(graph.Model.Frames);
            Assert.Contains(node, graph.Model.Nodes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AFrameCarriesASmallerOneInsideItAndNotTheOtherWayRound()
    {
        var graph = Open(out var window, out var outer, out _);
        var inner = graph.Model!.Add(new GraphFrame("inner", "Inner", Colors.MediumAquamarine)
        {
            X = outer.X + 40,
            Y = outer.Y + 60,
            Width = 120,
            Height = 90,
        });

        Settle(window);

        var wasInner = new Point(inner.X, inner.Y);
        var wasOuter = new Point(outer.X, outer.Y);

        try
        {
            var tab = graph.View.ToScreen(outer.LabelBox(GraphMetrics.Dense).Center);

            window.MouseDown(tab, MouseButton.Left);
            window.MouseMove(tab + new Vector(50, 30));
            window.MouseUp(tab + new Vector(50, 30), MouseButton.Left);
            Settle(window);

            Assert.Equal(wasInner.X + 50, inner.X, 3);
            Assert.Equal(wasInner.Y + 30, inner.Y, 3);

            // The other way round: the inner one does not take its parent with it.
            var small = graph.View.ToScreen(inner.LabelBox(GraphMetrics.Dense).Center);

            window.MouseDown(small, MouseButton.Left);
            window.MouseMove(small + new Vector(-20, 0));
            window.MouseUp(small + new Vector(-20, 0), MouseButton.Left);
            Settle(window);

            Assert.Equal(wasOuter.X + 50, outer.X, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ThePointerSaysWhichWayAnEdgeWouldPull()
    {
        var graph = Open(out var window, out var frame, out _);

        try
        {
            window.MouseMove(graph.View.ToScreen(Grip(frame, GraphEdges.Right)));
            Settle(window);

            Assert.Equal(StandardCursorType.SizeWestEast, Reading(graph));

            window.MouseMove(graph.View.ToScreen(Grip(frame, GraphEdges.Left | GraphEdges.Top)));
            Settle(window);

            Assert.Equal(StandardCursorType.TopLeftCorner, Reading(graph));

            // Well inside it there is nothing to pull, so the pointer says nothing and a box
            // select started in there still works.
            window.MouseMove(graph.View.ToScreen(frame.Bounds.Center));
            Settle(window);

            Assert.Equal(StandardCursorType.Arrow, Reading(graph));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClickingAwayKeepsWhatWasTyped()
    {
        var graph = Open(out var window, out var frame, out _);

        try
        {
            graph.BeginRename(frame);
            Settle(window);

            Field(graph).Text = "Typed and left";

            window.MouseDown(new Point(880, 620), MouseButton.Left);
            window.MouseUp(new Point(880, 620), MouseButton.Left);
            Settle(window);

            Assert.Equal("Typed and left", frame.Label);
            Assert.Null(graph.Renaming);
        }
        finally
        {
            window.Close();
        }
    }

    private static StandardCursorType Reading(NodeGraph graph)
    {
        var cursor = graph.Cursor?.ToString() ?? nameof(StandardCursorType.Arrow);

        return Enum.TryParse<StandardCursorType>(cursor, out var kind) ? kind : StandardCursorType.Arrow;
    }

    /// <summary>A point on the frame's edge or corner, in graph units.</summary>
    private static Point Grip(GraphFrame frame, GraphEdges edges)
    {
        var box = frame.Bounds;

        var x = edges.HasFlag(GraphEdges.Left) ? box.X
            : edges.HasFlag(GraphEdges.Right) ? box.Right
            : box.Center.X;

        var y = edges.HasFlag(GraphEdges.Top) ? box.Y
            : edges.HasFlag(GraphEdges.Bottom) ? box.Bottom
            : box.Center.Y;

        return new Point(x, y);
    }

    private static TextBox Field(NodeGraph graph) =>
        graph.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == NodeGraph.PartRename);

    private static NodeGraph Open(out Window window, out GraphFrame frame, out GraphNode node)
    {
        var model = new GraphModel();

        node = model.Add(new GraphNode("a", "Source", "data", 170));
        node.MoveTo(200, 220);
        node.AddOutput("Out", "Float");

        frame = model.Add(new GraphFrame("f", "Group", Colors.MediumPurple)
        {
            X = 160,
            Y = 180,
            Width = 300,
            Height = 200,
        });

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
