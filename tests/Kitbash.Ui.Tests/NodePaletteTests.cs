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
/// The add node menu. Most of this is about getting out of it: a menu a person cannot see how
/// to leave is a menu they close by picking something they did not want.
/// </summary>
public class NodePaletteTests
{
    private static readonly NodeChoice[] Catalogue =
    [
        new NodeChoice("Add", "math", "Math").With("A", "Float", "0").With("B", "Float", "0").Gives("Out", "Float"),
        new NodeChoice("Multiply", "math", "Math").With("A", "Float", "1").With("B", "Float", "1").Gives("Out", "Float"),
        new NodeChoice("Compare", "logic", "Logic").With("A", "Float").Gives("Result", "Bool"),
        new NodeChoice("And", "logic", "Logic").With("A", "Bool").With("B", "Bool").Gives("Result", "Bool"),
        new NodeChoice("Watch value", "debug", "Debug").With("Value", "Float"),
    ];

    [AvaloniaFact]
    public void TabOpensItAndEscapeLeavesWithNothingAdded()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.Focus();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Settle(window);

            Assert.True(graph.IsPaletteOpen);
            Assert.True(palette.IsVisible);

            Search(palette).Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Settle(window);

            Assert.False(graph.IsPaletteOpen);
            Assert.Equal(2, graph.Model!.Nodes.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EscapeOnTheCanvasClosesItBeforeItTouchesTheSelection()
    {
        var graph = Open(out var window);

        graph.Selection.Set(graph.Model!.Nodes[0]);
        Settle(window);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            graph.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Settle(window);

            // The menu went and the selection stayed. One key, one step back each time.
            Assert.False(graph.IsPaletteOpen);
            Assert.Equal(1, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APressOnTheCanvasClosesItAndDoesNothingElse()
    {
        var graph = Open(out var window);

        graph.Selection.Set(graph.Model!.Nodes[0]);
        Settle(window);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var away = new Point(760, 520);

            window.MouseDown(away, MouseButton.Left);
            window.MouseUp(away, MouseButton.Left);
            Settle(window);

            // Clicking away is the first thing a person tries. It has to close the menu and
            // it must not also start a box select or drop what was picked.
            Assert.False(graph.IsPaletteOpen);
            Assert.Equal(1, graph.Selection.Count);
            Assert.Equal(2, graph.Model.Nodes.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void APressInsideItNeverReachesTheCanvas()
    {
        var graph = Open(out var window);

        graph.Selection.Set(graph.Model!.Nodes[0]);
        Settle(window);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var palette = Palette(graph);
            var box = palette.Bounds;
            var inside = new Point(box.X + box.Width / 2, box.Y + box.Height - 6);

            window.MouseDown(inside, MouseButton.Left);
            Settle(window);

            Assert.True(graph.IsPaletteOpen);
            Assert.Equal(1, graph.Selection.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheCancelButtonLeaves()
    {
        var graph = Open(out var window);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var cancel = Palette(graph)
                .GetVisualDescendants()
                .OfType<Button>()
                .First(button => button.Name == NodePalette.PartCancel);

            cancel.Command?.Execute(null);
            Press(cancel);
            Settle(window);

            Assert.False(graph.IsPaletteOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TypingNarrowsItAndANameThatMatchesNothingSaysSo()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            Assert.False(palette.IsEmpty);
            Assert.Equal("Add", palette.Current?.Title);

            Search(palette).Text = "mult";
            Settle(window);

            Assert.False(palette.IsEmpty);
            Assert.Equal("Multiply", palette.Current?.Title);

            Search(palette).Text = "zzzz";
            Settle(window);

            Assert.True(palette.IsEmpty);
            Assert.Null(palette.Current);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OpenedOffAPinItOffersOnlyWhatThatPinReaches()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            var flag = graph.Model!.Nodes[0].Outputs[1];

            graph.AskForNode(new Point(200, 200), new Point(200, 200), flag);
            Settle(window);

            Assert.True(palette.IsFiltered);
            Assert.Equal("Showing nodes that take Bool", palette.FilterText);

            // Only And takes a Bool. Everything else is left out rather than offered and
            // refused after the fact.
            Assert.Equal("And", palette.Current?.Title);

            palette.Step(1);
            Assert.Equal("And", palette.Current?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheArrowsWalkTheEntriesAndStepOverTheHeadings()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var walked = new List<string?>();

            for (var step = 0; step < 6; step++)
            {
                walked.Add(palette.Current?.Title);
                palette.Step(1);
            }

            Assert.Equal(["Add", "Multiply", "Compare", "And", "Watch value", "Watch value"], walked);

            palette.Step(-2);
            Assert.Equal("Compare", palette.Current?.Title);

            palette.Step(-99);
            Assert.Equal("Add", palette.Current?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PickingAddsTheNodeWiredAndPlacedWhereTheWireWasLetGo()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            var source = graph.Model!.Nodes[0].Outputs[0];
            var at = new Point(400, 320);

            graph.AskForNode(at, graph.View.ToScreen(at), source);
            Settle(window);

            palette.Commit();
            Settle(window);

            Assert.False(graph.IsPaletteOpen);
            Assert.Equal(3, graph.Model.Nodes.Count);

            var made = graph.Model.Nodes[^1];
            var link = Assert.Single(graph.Model.Links);

            Assert.Same(source, link.From);
            Assert.Same(made.Inputs[0], link.To);
            Assert.Contains(made, graph.Selection.Items);

            // The pin the wire lands on sits where the wire was let go, so the node arrives
            // joined at the spot a person aimed at rather than beside it.
            var pin = made.PortPoint(made.Inputs[0]);

            Assert.Equal(at.X, pin.X, 1);
            Assert.Equal(at.Y, pin.Y, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void WithNoCatalogueNothingOpensAndTheAppIsAskedInstead()
    {
        var graph = Open(out var window);
        var asks = new List<GraphPaletteEventArgs>();

        graph.Catalogue = [];
        graph.PaletteAsked += (_, e) => asks.Add(e);

        try
        {
            graph.AskForNode(new Point(10, 20), new Point(30, 40));
            Settle(window);

            Assert.False(graph.IsPaletteOpen);

            var ask = Assert.Single(asks);

            Assert.Equal(new Point(10, 20), ask.At);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItOpensInsideTheCanvasHoweverCloseToAnEdgeItWasAskedFor()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(default, new Point(graph.Bounds.Width - 4, graph.Bounds.Height - 4));
            Settle(window);

            var box = palette.Bounds;

            Assert.True(box.Right <= graph.Bounds.Width, $"{box} runs past {graph.Bounds.Width}");
            Assert.True(box.Bottom <= graph.Bounds.Height, $"{box} runs past {graph.Bounds.Height}");
            Assert.True(box.X >= 0 && box.Y >= 0, $"{box} starts outside the canvas");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(Button button)
    {
        var press = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        press!.Invoke(button, null);
    }

    private static NodePalette Palette(NodeGraph graph) =>
        graph.GetVisualDescendants().OfType<NodePalette>().First();

    private static TextBox Search(NodePalette palette) =>
        palette.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == NodePalette.PartSearch);

    private static NodeGraph Open(out Window window)
    {
        var model = new GraphModel();
        var from = model.Add(new GraphNode("a", "Source", "data", 170));
        var to = model.Add(new GraphNode("b", "Sink", "out", 170));

        from.MoveTo(80, 120);
        to.MoveTo(520, 200);
        from.AddOutput("Out", "Float");
        from.AddOutput("Flag", "Bool");
        to.AddInput("In", "Float");

        var graph = new NodeGraph
        {
            Model = model,
            Catalogue = Catalogue,
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
