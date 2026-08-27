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

public class NodePaletteProbeTests
{
    private static readonly NodeChoice[] Catalogue =
    [
        new NodeChoice("Add", "math", "Math").With("A", "Float", "0").Gives("Out", "Float"),
        new NodeChoice("Multiply", "math", "Math").With("A", "Float", "1").Gives("Out", "Float"),
        new NodeChoice("Compare", "logic", "Logic").With("A", "Float").Gives("Result", "Bool"),
    ];

    [AvaloniaFact]
    public void ProbeANameThatMatchesNothingDrawsNoRows()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            Search(palette).Text = "zzzz";
            Settle(window);

            var rows = palette.GetVisualDescendants().OfType<ListBoxItem>().Where(row => row.IsVisible).ToList();

            Assert.True(palette.IsEmpty);
            Assert.Empty(rows);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProbeTypingDoesNotReachTheGraphsOwnKeys()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.Selection.Set(graph.Model!.Nodes.First());
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var box = Search(palette);

            box.Focus();
            Settle(window);

            var zoom = graph.View.Zoom;
            var offset = graph.View.Offset;

            // A person typing a node name. Every one of these is a graph shortcut.
            foreach (var key in new[] { PhysicalKey.F, PhysicalKey.A, PhysicalKey.D })
            {
                window.KeyPressQwerty(key, RawInputModifiers.None);
                Settle(window);
            }

            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Settle(window);

            Assert.Equal(zoom, graph.View.Zoom);
            Assert.Equal(offset, graph.View.Offset);
            Assert.Equal(2, graph.Model!.Nodes.Count);
            Assert.Single(graph.Selection.Nodes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProbeRenamingDoesNotReachTheGraphsOwnKeys()
    {
        var graph = Open(out var window);

        try
        {
            var node = graph.Model!.Nodes.First();

            graph.Selection.Set(node);
            graph.BeginRename(node);
            Settle(window);

            var zoom = graph.View.Zoom;
            var offset = graph.View.Offset;

            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Settle(window);

            Assert.Equal(zoom, graph.View.Zoom);
            Assert.Equal(offset, graph.View.Offset);
            Assert.Equal(2, graph.Model!.Nodes.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProbeTheBestMatchIsFirstAndTheHeadingsGoWithTheQuery()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            // Nothing typed, so it is the catalogue grouped the way it was handed over.
            Assert.Contains(Rows(palette), row => row is NodeChoiceHeading);

            // Compare holds it, Add starts with it, so Add is the one Enter would take.
            Search(palette).Text = "ad";
            Settle(window);

            Assert.Equal("Add", palette.Current?.Title);
            Assert.DoesNotContain(Rows(palette), row => row is NodeChoiceHeading);

            // A word inside a name, not the start of it.
            Search(palette).Text = "pare";
            Settle(window);

            Assert.Equal("Compare", palette.Current?.Title);

            // The group, which is the weakest match of all and still a match.
            Search(palette).Text = "logic";
            Settle(window);

            Assert.Equal("Compare", palette.Current?.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProbeClearingTheQueryPutsEveryNodeBack()
    {
        var graph = Open(out var window);
        var palette = Palette(graph);

        try
        {
            graph.AskForNode(new Point(200, 200), new Point(200, 200));
            Settle(window);

            var all = Rows(palette).Count;

            Search(palette).Text = "mult";
            Settle(window);

            Assert.True(Rows(palette).Count < all);

            Search(palette).Clear();
            Settle(window);

            Assert.Equal(all, Rows(palette).Count);
            Assert.False(palette.IsEmpty);
        }
        finally
        {
            window.Close();
        }
    }

    private static IReadOnlyList<object> Rows(NodePalette palette) =>
    [
        .. palette.GetVisualDescendants().OfType<ListBox>()
            .First(list => list.Name == NodePalette.PartRows)
            .ItemsSource!.Cast<object>(),
    ];

    /// <summary>Draws the menu to a file so it can be looked at. Nothing unless KB_SHOT names one.</summary>
    [AvaloniaFact]
    public void Draw()
    {
        if (Environment.GetEnvironmentVariable("KB_SHOT") is not { } into)
        {
            return;
        }

        var graph = Open(out var window);
        var palette = Palette(graph);

        graph.AskForNode(new Point(60, 40), new Point(60, 40));
        Settle(window);

        if (Environment.GetEnvironmentVariable("KB_SHOT_QUERY") is { } typed)
        {
            Search(palette).Text = typed;

            for (var pass = 0; pass < 3; pass++)
            {
                Settle(window);
            }
        }

        using var frame = window.CaptureRenderedFrame();

        if (frame is not null)
        {
            using var file = File.Create(into);

            frame.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        window.Close();
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
