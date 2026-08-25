using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;


namespace Kitbash.Ui.Tests;

/// <summary>
/// The claim the whole control is built on, measured: a graph of two thousand nodes pans and
/// draws at the cost of what is on screen rather than the cost of what is in it.
/// </summary>
public class NodeGraphScaleTests(ITestOutputHelper output)
{
    private const int Across = 50;
    private const int Down = 40;

    [AvaloniaFact]
    public void TwoThousandNodesPanWithoutRealisingThem()
    {
        var model = Big();
        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Shapes, Brushes.CornflowerBlue)),
        };

        var window = new Window { Width = 1600, Height = 900, Content = graph };

        window.Show();
        Settle(window);

        try
        {
            var panel = graph.GetVisualDescendants().OfType<NodeGraphPanel>().First();

            Assert.Equal(Across * Down, model.Nodes.Count);

            graph.View.Zoom = 1;
            graph.View.Offset = default;
            Settle(window);

            var most = 0;
            var settle = System.Diagnostics.Stopwatch.StartNew();

            // Twelve pixels a frame, which is an ordinary drag rather than a teleport.
            for (var step = 0; step < 240; step++)
            {
                graph.View.Offset = new Vector(-step * 12, -step * 5);
                Settle(window);
                most = Math.Max(most, panel.RealisedCount);
            }

            settle.Stop();

            output.WriteLine($"{model.Nodes.Count} nodes, {model.Links.Count} links");
            output.WriteLine($"most realised at once: {most}");
            output.WriteLine($"per pan frame, realise and lay out: {settle.Elapsed.TotalMilliseconds / 240:F2} ms");
            output.WriteLine($"per frame drawn: {Draw(graph, 60):F2} ms");

            // Every pointer move hit tests the model, so that is the cost of simply moving
            // the pointer across a graph this size.
            var hover = System.Diagnostics.Stopwatch.StartNew();

            for (var step = 0; step < 400; step++)
            {
                window.MouseMove(new Point(200 + step % 900, 150 + step % 500));
            }

            hover.Stop();
            output.WriteLine($"per pointer move, hit tested: {hover.Elapsed.TotalMilliseconds / 400:F3} ms");

            var asked = System.Diagnostics.Stopwatch.StartNew();

            for (var step = 0; step < 4000; step++)
            {
                graph.HitTest(graph.View.ToGraph(new Point(200 + step % 900, 150 + step % 500)));
            }

            asked.Stop();
            output.WriteLine($"one hit test against the model: {asked.Elapsed.TotalMilliseconds / 4000:F4} ms");

            var bare = new NodeGraph { Model = new GraphModel() };

            window.Content = bare;
            Settle(window);

            var idle = System.Diagnostics.Stopwatch.StartNew();

            for (var step = 0; step < 400; step++)
            {
                window.MouseMove(new Point(200 + step % 900, 150 + step % 500));
            }

            idle.Stop();
            output.WriteLine($"per pointer move over an empty graph: {idle.Elapsed.TotalMilliseconds / 400:F3} ms");

            window.Content = graph;
            Settle(window);

            graph.ShowGrid = false;
            Settle(window);
            output.WriteLine($"per frame drawn with no grid: {Draw(graph, 60):F2} ms");

            var empty = new NodeGraph { Model = new GraphModel(), ShowGrid = false };

            window.Content = empty;
            Settle(window);
            output.WriteLine($"an empty canvas, so the raster alone: {Draw(empty, 60):F2} ms");

            Assert.InRange(most, 1, 90);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ZoomedRightOutTheWholeGraphIsBoxesAndNoContainers()
    {
        var model = Big();
        var graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Shapes, Brushes.CornflowerBlue)),
        };

        var window = new Window { Width = 1600, Height = 900, Content = graph };

        window.Show();
        Settle(window);

        try
        {
            var panel = graph.GetVisualDescendants().OfType<NodeGraphPanel>().First();

            graph.Fit();
            Settle(window);

            output.WriteLine($"zoom {graph.View.Zoom:F3}, lod {graph.View.Lod}");
            output.WriteLine($"realised: {panel.RealisedCount}");
            output.WriteLine($"per frame drawn with the whole graph in view: {Draw(graph, 30):F2} ms");

            Assert.Equal(GraphLod.Block, graph.View.Lod);
            Assert.Equal(0, panel.RealisedCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// How long the canvas takes to draw, averaged. Rendered into a bitmap rather than
    /// captured off the window, so what is timed is Skia drawing the graph and not a
    /// readback of the whole framebuffer.
    /// </summary>
    private static double Draw(NodeGraph graph, int frames)
    {
        using var target = new RenderTargetBitmap(new PixelSize(1600, 900));

        target.Render(graph);

        var clock = System.Diagnostics.Stopwatch.StartNew();

        for (var frame = 0; frame < frames; frame++)
        {
            target.Render(graph);
        }

        clock.Stop();

        return clock.Elapsed.TotalMilliseconds / frames;
    }

    private static GraphModel Big()
    {
        var model = new GraphModel();

        for (var y = 0; y < Down; y++)
        {
            for (var x = 0; x < Across; x++)
            {
                var node = new GraphNode($"n{y}_{x}", $"Node {y}.{x}", "math", 172);

                node.MoveTo(x * 240, y * 170);
                node.AddInput("A", "Float", "0");
                node.AddInput("B", "Float", "1");
                node.AddOutput("Out", "Float");

                model.Add(node);

                if (x > 0)
                {
                    model.Add(new GraphLink(model.Nodes[^2].Outputs[0], node.Inputs[0]));
                }
            }
        }

        return model;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
