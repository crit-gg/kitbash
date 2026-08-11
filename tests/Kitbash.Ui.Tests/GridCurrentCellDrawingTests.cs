using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The current cell mark is drawn inside the cell on all four edges. It is a one pixel
/// border and the cell clips to its own bounds, so an edge landing on the boundary is the
/// thing that goes missing.
/// </summary>
public sealed class GridCurrentCellDrawingTests
{
    /// <summary>The two colours the mark takes, off a picked row and on one.</summary>
    private static readonly Color[] Marks = [Color.Parse("#569eff"), Color.Parse("#9cc6ff")];

    [AvaloniaFact]
    public void AllFourEdgesOfTheMarkAreDrawn()
    {
        var grid = Grid(out var window);

        try
        {
            var wrong = new List<string>();

            // Every cell, since the edges of the grid are where a mark can lose one: the
            // header above, the footer below, the frame beside it.
            for (var row = 0; row < grid.ItemCount; row++)
            {
                for (var column = 0; column < grid.Columns.Count; column++)
                {
                    grid.SelectedIndex = row;
                    Dispatcher.UIThread.RunJobs();

                    for (var step = 0; step < column; step++)
                    {
                        Key(grid, Avalonia.Input.Key.Right);
                    }

                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var cell = Cells(grid).SingleOrDefault(cell => cell.IsCurrent);

                    if (cell is null)
                    {
                        wrong.Add($"row {row} column {column} has no mark");
                        continue;
                    }

                    var box = Box(cell, window);
                    var frame = Pixels(window);

                    var missing = new[] { Edge.Top, Edge.Bottom, Edge.Left, Edge.Right }
                        .Where(edge => !Runs(frame, box, edge))
                        .ToList();

                    if (missing.Count > 0)
                    {
                        wrong.Add($"row {row} column {column} in {box} missing {string.Join(" and ", missing)}. {Scan(frame, box)}");
                    }
                }
            }

            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Key(Control target, Avalonia.Input.Key key)
    {
        target.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = key,
            Source = target,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right,
    }

    /// <summary>Which rows and columns near the edges carry the mark, for a failure message.</summary>
    private static string Scan(Frame frame, PixelRect box)
    {
        var middle = box.X + (box.Width / 2);
        var side = box.Y + (box.Height / 2);

        var top = Line(step => frame.At(middle, box.Y + step));
        var bottom = Line(step => frame.At(middle, box.Y + box.Height - 1 + step));
        var left = Line(step => frame.At(box.X + step, side));
        var right = Line(step => frame.At(box.X + box.Width - 1 + step, side));

        return $"top {top} bottom {bottom} left {left} right {right}";

        static string Line(Func<int, Color> read) =>
            string.Join(" ", Enumerable.Range(-2, 5).Select(step => $"{step:+0;-0;0}:{read(step)}"));
    }

    /// <summary>Whether most of one edge of the box carries the mark colour.</summary>
    private static bool Runs(Frame frame, PixelRect box, Edge edge)
    {
        var hit = 0;
        var seen = 0;

        if (edge is Edge.Top or Edge.Bottom)
        {
            var y = edge == Edge.Top ? box.Y : box.Y + box.Height - 1;

            for (var x = box.X + 2; x < box.X + box.Width - 2; x++)
            {
                seen++;
                hit += Near(frame.At(x, y)) ? 1 : 0;
            }
        }
        else
        {
            var x = edge == Edge.Left ? box.X : box.X + box.Width - 1;

            for (var y = box.Y + 2; y < box.Y + box.Height - 2; y++)
            {
                seen++;
                hit += Near(frame.At(x, y)) ? 1 : 0;
            }
        }

        return seen > 0 && hit == seen;
    }

    /// <summary>
    /// The mark's own colour, near enough for the renderer. A shaved edge blends with what
    /// is under it and lands well outside this, which is the whole point of the test.
    /// </summary>
    private static bool Near(Color colour) => Marks.Any(mark =>
        Math.Abs(colour.R - mark.R) <= 4
        && Math.Abs(colour.G - mark.G) <= 4
        && Math.Abs(colour.B - mark.B) <= 4);

    private static PixelRect Box(Visual cell, Visual window)
    {
        var corner = cell.TranslatePoint(default, window) ?? default;

        return new PixelRect(
            (int)Math.Round(corner.X),
            (int)Math.Round(corner.Y),
            (int)Math.Round(cell.Bounds.Width),
            (int)Math.Round(cell.Bounds.Height));
    }

    private static Frame Pixels(Window window)
    {
        using var shot = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the window drew nothing");

        using var locked = shot.Lock();

        var bytes = new byte[locked.RowBytes * locked.Size.Height];

        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);

        return new Frame(bytes, locked.RowBytes, locked.Size, locked.Format);
    }

    private static IEnumerable<DataGridCell> Cells(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().Where(cell => cell.IsVisible);

    private static DataGrid Grid(out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(Column("ID"));
        grid.Columns.Add(Column("NAME"));
        grid.Columns.Add(Column("KIND"));

        grid.ItemsSource = new GridRows(new[]
        {
            new Entry("one"),
            new Entry("two"),
            new Entry("three"),
        });

        window = new Window { Content = grid, Width = 520, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header) => new()
    {
        Header = header,
        Width = new GridLength(1, GridUnitType.Star),
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
    };

    private sealed record Entry(string Name);

    /// <summary>One captured frame, read back a pixel at a time.</summary>
    private sealed class Frame(byte[] bytes, int stride, PixelSize size, PixelFormat format)
    {
        public Color At(int x, int y)
        {
            if (x < 0 || y < 0 || x >= size.Width || y >= size.Height)
            {
                return Colors.Transparent;
            }

            var at = (y * stride) + (x * 4);

            return format == PixelFormat.Rgba8888
                ? Color.FromArgb(bytes[at + 3], bytes[at], bytes[at + 1], bytes[at + 2])
                : Color.FromArgb(bytes[at + 3], bytes[at + 2], bytes[at + 1], bytes[at]);
        }
    }
}
