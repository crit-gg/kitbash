using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A node's edge is one unbroken ring. It is drawn last for that reason: the header's own
/// fill covers the half of the stroke that falls inside the card, so drawn first the ring
/// appeared to start below the header.
/// </summary>
public class NodeCardDrawingTests
{
    private const int Wide = 460;
    private const int Tall = 220;

    /// <summary>Where the node is put, at whole pixels so the stroke lands predictably.</summary>
    private static readonly Point At = new(60, 40);

    [AvaloniaFact]
    public void ThePickedRingRunsPastTheHeaderRatherThanStartingUnderIt()
    {
        var node = Drawn(picked: true, collapsed: false, out var frame);

        using (frame)
        {
            var header = GraphMetrics.Dense.HeaderHeight;
            var thin = new List<int>();

            // Every row of the card, the header band included. The edge has to be there for
            // all of it, and before the fix the header band had none of it at all.
            for (var y = 4; y < node.Height - 4; y++)
            {
                // A pin sits on the edge by design and is drawn in its own type's colour, so
                // the rows it covers are not rows where the edge should be showing.
                if (Pinned(node, y))
                {
                    continue;
                }

                var left = Strongest(frame, (int)node.X, (int)(node.Y + y));
                var right = Strongest(frame, (int)(node.X + node.Width), (int)(node.Y + y));

                if (left < 40 || right < 40)
                {
                    thin.Add(y);
                }
            }

            Assert.True(thin.Count < node.Height, "every row was pinned, so nothing was measured");

            Assert.True(
                thin.Count == 0,
                $"the edge is missing at {string.Join(", ", thin)}, and the header is the first {header}");
        }
    }

    [AvaloniaFact]
    public void TheRingClosesAcrossTheTopAndTheBottom()
    {
        var node = Drawn(picked: true, collapsed: false, out var frame);

        using (frame)
        {
            var gaps = new List<int>();

            // Inside the corner radius at either end, so what is measured is the straight run
            // rather than the curve.
            for (var x = 12; x < node.Width - 12; x++)
            {
                var top = Strongest(frame, (int)(node.X + x), (int)node.Y);
                var bottom = Strongest(frame, (int)(node.X + x), (int)(node.Y + node.Height));

                if (top < 40 || bottom < 40)
                {
                    gaps.Add(x);
                }
            }

            Assert.True(gaps.Count == 0, $"the edge is missing at {string.Join(", ", gaps)}");
        }
    }

    [AvaloniaFact]
    public void ACollapsedNodeKeepsItsBottomCorners()
    {
        var node = Drawn(picked: false, collapsed: true, out var frame);

        using (frame)
        {
            // The corner of the box a rounded card leaves empty. The header used to fill the
            // whole card with square corners, so this pixel wore the header's own tone.
            var corner = Pixel(frame, (int)node.X + 1, (int)(node.Y + node.Height) - 1);
            var middle = Pixel(frame, (int)(node.X + node.Width / 2), (int)(node.Y + node.Height / 2));

            Assert.NotEqual(middle, corner);
        }
    }

    /// <summary>Whether a pin is drawn over this row of the card's edge.</summary>
    private static bool Pinned(GraphNode node, double y)
    {
        var metrics = GraphMetrics.Dense;
        var reach = metrics.PinRadius + metrics.PinRing + 2;

        foreach (var port in node.Inputs.Concat(node.Outputs))
        {
            if (Math.Abs(metrics.PortOffset(node, port).Y - y) <= reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How much of the accent a pixel carries, over a three wide window.</summary>
    private static int Strongest(Frame frame, int x, int y)
    {
        var most = 0;

        for (var step = -1; step <= 1; step++)
        {
            var pixel = Pixel(frame, x + step, y);

            // The accent is blue and everything it could be drawn over is grey, so the
            // distance between the two channels is what tells them apart.
            most = Math.Max(most, ((pixel >> 16) & 0xff) - (pixel & 0xff));
        }

        return most;
    }

    private static int Pixel(Frame frame, int x, int y) =>
        x < 0 || y < 0 || x >= frame.Width || y >= frame.Height
            ? 0
            : BitConverter.ToInt32(frame.Bytes, y * frame.Stride + x * 4);

    private static GraphNode Drawn(bool picked, bool collapsed, out Frame frame)
    {
        var model = new GraphModel();
        var node = new GraphNode("n", "A node", "math", 180);

        node.MoveTo(At.X, At.Y);
        node.AddInput("A", "Float", "0");
        node.AddOutput("Out", "Float");
        node.IsCollapsed = collapsed;
        model.Add(node);

        var graph = new NodeGraph
        {
            Model = model,
            ShowGrid = false,
            Kinds = new GraphKinds(new GraphKind(IconGlyph.Shapes, Brushes.CornflowerBlue)),
            Ports = new PortPalette([Brushes.MediumAquamarine]),
        };

        var window = new Window { Width = Wide, Height = Tall, Content = graph };

        window.Show();
        Settle(window);

        graph.View.Zoom = 1;
        graph.View.Offset = default;
        node.IsSelected = picked;
        Settle(window);

        var shot = window.CaptureRenderedFrame()!;

        using var buffer = shot.Lock();

        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];

        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

        frame = new Frame(bytes, buffer.RowBytes, buffer.Size.Width, buffer.Size.Height, window);
        return node;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record Frame(byte[] Bytes, int Stride, int Width, int Height, Window Window) : IDisposable
    {
        public void Dispose() => Window.Close();
    }
}
