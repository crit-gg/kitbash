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
/// The map. A press goes there, and holding on carries the view with the pointer, since a map
/// a person can only tap is a map they have to tap over and over.
/// </summary>
public class NodeMinimapTests
{
    [AvaloniaFact]
    public void APressOutsideTheViewGoesThere()
    {
        var map = Open(out var window, out var graph);

        try
        {
            var was = graph.View.Offset;

            window.MouseDown(At(map, 0.15, 0.2), MouseButton.Left);
            window.MouseUp(At(map, 0.15, 0.2), MouseButton.Left);
            Settle(window);

            Assert.NotEqual(was, graph.View.Offset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HoldingOnCarriesTheViewWithThePointer()
    {
        var map = Open(out var window, out var graph);

        try
        {
            window.MouseDown(At(map, 0.2, 0.2), MouseButton.Left);
            Settle(window);

            Assert.True(map.IsDragging);

            var after = graph.View.Offset;

            window.MouseMove(At(map, 0.8, 0.8));
            Settle(window);

            // The view kept moving while the button was held, rather than stopping where the
            // press landed.
            Assert.NotEqual(after, graph.View.Offset);

            window.MouseUp(At(map, 0.8, 0.8), MouseButton.Left);
            Settle(window);

            Assert.False(map.IsDragging);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PressingInsideTheViewTakesHoldWithoutJumping()
    {
        var map = Open(out var window, out var graph);

        try
        {
            // Middle of the map is where the view sits after a fit, so a press there is a
            // press on the view itself.
            var was = graph.View.Offset;

            window.MouseDown(At(map, 0.5, 0.5), MouseButton.Left);
            Settle(window);

            Assert.Equal(was.X, graph.View.Offset.X, 1);
            Assert.Equal(was.Y, graph.View.Offset.Y, 1);

            window.MouseUp(At(map, 0.5, 0.5), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point At(NodeMinimap map, double across, double down)
    {
        var box = map.Bounds;

        return new Point(box.X + box.Width * across, box.Y + box.Height * down);
    }

    private static NodeMinimap Open(out Window window, out NodeGraph graph)
    {
        var model = new GraphModel();

        for (var step = 0; step < 12; step++)
        {
            var node = new GraphNode($"n{step}", $"Node {step}", "math", 170);

            node.MoveTo(step * 260, step % 4 * 200);
            node.AddInput("A", "Float", "0");
            node.AddOutput("Out", "Float");
            model.Add(node);
        }

        graph = new NodeGraph
        {
            Model = model,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Cube, Brushes.White)),
        };

        var map = new NodeMinimap
        {
            Graph = graph,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(12),
        };

        window = new Window { Width = 1000, Height = 700, Content = new Panel { Children = { graph, map } } };
        window.Show();
        Settle(window);

        graph.Fit();
        Settle(window);

        return map;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
