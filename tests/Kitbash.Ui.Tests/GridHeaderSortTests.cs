using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Clicking a column title. The header cell used to name the flat grid, so the same title
/// in a tree grid showed the hand cursor and did nothing at all when it was pressed.
/// </summary>
public sealed class GridHeaderSortTests
{
    [AvaloniaFact]
    public void TheFlatGridSortsThroughItsHeader()
    {
        var grid = new DataGrid();
        var column = Column();

        grid.Columns.Add(column);
        grid.ItemsSource = new GridRows(new[] { new Entry("item10"), new Entry("item2"), new Entry("item1") });

        var window = Shown(grid);

        try
        {
            Click(grid);

            Assert.Equal(GridSortDirection.Ascending, column.SortDirection);
            Assert.Equal(["item1", "item2", "item10"], Names(grid.Rows!.Select(row => row.Item)));

            Click(grid);

            Assert.Equal(GridSortDirection.Descending, column.SortDirection);
            Assert.Equal(["item10", "item2", "item1"], Names(grid.Rows!.Select(row => row.Item)));

            // The third press puts the source order back, so a person can always return.
            Click(grid);

            Assert.Equal(GridSortDirection.None, column.SortDirection);
            Assert.Equal(["item10", "item2", "item1"], Names(grid.Rows!.Select(row => row.Item)));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The same title in a tree grid, which is the one that did nothing. Sorting a
    /// hierarchy orders siblings under each parent rather than flattening it.
    /// </summary>
    [AvaloniaFact]
    public void TheTreeGridSortsSiblingsThroughItsHeader()
    {
        var roots = new[]
        {
            new Entry("beta", new Entry("b2"), new Entry("b10"), new Entry("b1")),
            new Entry("alpha"),
        };

        var grid = new TreeDataGrid();
        var column = Column();

        grid.Columns.Add(column);
        grid.ItemsSource = new TreeRows(roots, item => ((Entry)item).Children);

        var window = Shown(grid);

        try
        {
            Click(grid);

            Assert.Equal(GridSortDirection.Ascending, column.SortDirection);

            var rows = (TreeRows)grid.ItemsSource!;

            Assert.Equal(["alpha", "beta"], Names(rows.Select(row => row.Item)));

            // And a branch opened after the sort takes it too, since expanding builds its
            // rows the same way.
            rows.Toggle(rows.Single(row => ((Entry)row.Item).Name == "beta"));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(
                ["alpha", "beta", "b1", "b2", "b10"],
                Names(rows.Select(row => row.Item)));
        }
        finally
        {
            window.Close();
        }
    }

    private static GridColumn Column()
    {
        var column = new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        };

        column.SortKey = item => ((Entry)item).Name;

        return column;
    }

    /// <summary>A left press and release on the one header cell, the way a person sorts.</summary>
    private static void Click(Control grid)
    {
        var cell = grid.GetVisualDescendants().OfType<DataGridHeaderCell>().First(cell => cell.IsVisible);

        cell.RaiseEvent(new PointerReleasedEventArgs(
            cell,
            new Pointer(0, PointerType.Mouse, true),
            cell,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));

        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<string> Names(IEnumerable<object?> items) =>
        items.OfType<Entry>().Select(entry => entry.Name);

    private static Window Shown(Control grid)
    {
        var window = new Window { Content = grid, Width = 420, Height = 260 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return window;
    }

    private sealed class Entry(string name, params Entry[] children)
    {
        public string Name => name;

        public Entry[] Children => children;
    }
}
