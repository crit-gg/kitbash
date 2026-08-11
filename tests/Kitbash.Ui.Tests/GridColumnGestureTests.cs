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
/// What a person may do to the columns. Each gesture needs the grid to allow it and the
/// column to allow it, so a grid saying yes and a column saying no still means no.
/// </summary>
public sealed class GridColumnGestureTests
{
    /// <summary>A double click on the edge fits the column to what is on screen.</summary>
    [AvaloniaFact]
    public void FittingTakesTheWidestCellOnScreen()
    {
        var grid = Grid(out var window);

        try
        {
            var column = grid.Columns[0];
            var was = column.ActualWidth;

            Fit(grid, column);

            Assert.NotEqual(was, column.ActualWidth);

            // Wide enough for the longest value drawn, and nowhere near the star width it
            // had before.
            Assert.True(column.ActualWidth < was, $"fitted to {column.ActualWidth} from {was}");
            Assert.True(column.ActualWidth >= column.MinWidth);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told it may not fit does nothing when the edge is double clicked.</summary>
    [AvaloniaFact]
    public void AGridThatMayNotFitDoesNothing()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Resize;
            Dispatcher.UIThread.RunJobs();

            var column = grid.Columns[0];
            var was = column.ActualWidth;

            Fit(grid, column);

            Assert.Equal(was, column.ActualWidth);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told it may do nothing to its columns draws no grab area at all.</summary>
    [AvaloniaFact]
    public void NoGesturesLeavesNoEdgeToGrab()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.NotEmpty(Dividers(grid));

            grid.ColumnGestures = ColumnGestures.None;
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            Assert.Empty(Dividers(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Shift and a click adds a key rather than replacing the one already there.</summary>
    [AvaloniaFact]
    public void ShiftAndAClickAddsASortKey()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.MultiSort;
            Dispatcher.UIThread.RunJobs();

            Click(grid, grid.Columns[0]);
            Click(grid, grid.Columns[1], shift: true);

            Assert.Equal(2, grid.Rows!.Sorts.Count);
            Assert.Equal(1, grid.Columns[0].SortOrder);
            Assert.Equal(2, grid.Columns[1].SortOrder);
            Assert.Equal("sorted by ID, NAME", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Without the gesture a shift click replaces the key, which is what it always did.</summary>
    [AvaloniaFact]
    public void WithoutTheGestureAShiftClickReplaces()
    {
        var grid = Grid(out var window);

        try
        {
            Click(grid, grid.Columns[0]);
            Click(grid, grid.Columns[1], shift: true);

            Assert.Single(grid.Rows!.Sorts);
            Assert.Equal("sorted by NAME", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Past three keys the footer stops naming them.</summary>
    [AvaloniaFact]
    public void TheFooterStopsNamingPastThree()
    {
        var grid = Grid(out var window, columns: 5);

        try
        {
            grid.ColumnGestures = ColumnGestures.MultiSort;
            Dispatcher.UIThread.RunJobs();

            Click(grid, grid.Columns[0]);

            for (var at = 1; at < 5; at++)
            {
                Click(grid, grid.Columns[at], shift: true);
            }

            Assert.Equal("sorted by ID, NAME, COLUMN 2 and 2 more", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A left click on a column's title, which is the only way in from outside.</summary>
    private static void Click(DataGrid grid, GridColumn column, bool shift = false)
    {
        var title = grid.GetVisualDescendants()
            .OfType<DataGridHeaderCell>()
            .First(cell => ReferenceEquals(cell.Column, column));

        title.RaiseEvent(new PointerReleasedEventArgs(
            title,
            new Pointer(0, PointerType.Mouse, true),
            title,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            shift ? KeyModifiers.Shift : KeyModifiers.None,
            MouseButton.Left));

        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<ColumnDivider> Dividers(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<ColumnDivider>().Where(divider => divider.IsVisible);

    /// <summary>A double click on the edge that belongs to a column.</summary>
    private static void Fit(DataGrid grid, GridColumn column)
    {
        var divider = grid.GetVisualDescendants()
            .OfType<ColumnDivider>()
            .First(divider => ReferenceEquals(GridCells.GetColumn(divider), column));

        divider.RaiseEvent(new PointerPressedEventArgs(
            divider,
            new Pointer(0, PointerType.Mouse, true),
            divider,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None,
            2));

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGrid Grid(out Window window, int columns = 3)
    {
        var grid = new DataGrid();

        string[] names = ["ID", "NAME", "COLUMN 2", "COLUMN 3", "COLUMN 4"];

        for (var at = 0; at < columns; at++)
        {
            var name = names[at];

            grid.Columns.Add(new GridColumn
            {
                Header = name,
                Width = new GridLength(1, GridUnitType.Star),
                CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
                SortKey = item => ((Entry)item).Name,
            });
        }

        grid.ItemsSource = new GridRows(
            Enumerable.Range(0, 3).Select(number => new Entry($"r{number}")).ToList());

        window = new Window { Content = grid, Width = 720, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Name);
}
