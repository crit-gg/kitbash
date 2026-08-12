using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A block of cells. It is a mode, since shift and a click already mean extend the row
/// selection, and the row is what every grid in the launcher picks.
/// </summary>
public sealed class GridRangeTests
{
    /// <summary>In row units there is no block at all and shift is the list's own.</summary>
    [AvaloniaFact]
    public void RowUnitsHaveNoBlock()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);

            Assert.Empty(InRange(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Shift and the arrows take the block out from the anchor.</summary>
    [AvaloniaFact]
    public void ShiftAndArrowsTakeTheBlockOut()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);

            // Two rows by two columns, less the anchor, which wears no wash.
            Assert.Equal(3, InRange(grid).Count);

            var anchor = Cell(grid, 0, "ID");

            Assert.True(anchor.IsCurrent);
            Assert.False(anchor.IsInRange);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The outline is on the block's own edges and nowhere inside it.</summary>
    [AvaloniaFact]
    public void TheOutlineIsOnTheEdgesOfTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Right, KeyModifiers.Shift);

            // One row of three, so every cell keeps the top and bottom and only the ends
            // carry a side.
            Assert.Equal(new Thickness(1, 1, 0, 1), Cell(grid, 0, "ID").RangeEdges);
            Assert.Equal(new Thickness(0, 1, 0, 1), Cell(grid, 0, "NAME").RangeEdges);
            Assert.Equal(new Thickness(0, 1, 1, 1), Cell(grid, 0, "KIND").RangeEdges);

            // And a row outside the block carries none of it.
            Assert.Equal(default, Cell(grid, 1, "NAME").RangeEdges);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A range of one cell is the current cell mark, unchanged.</summary>
    [AvaloniaFact]
    public void ABlockOfOneIsTheCurrentCellMark()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new Thickness(1), Cell(grid, 1, "ID").RangeEdges);
            Assert.Empty(InRange(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Moving without shift puts the block back to the anchor alone.</summary>
    [AvaloniaFact]
    public void MovingWithoutShiftCollapsesTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);

            Assert.NotEmpty(InRange(grid));

            Press(grid, Key.Right);

            Assert.Empty(InRange(grid));
            Assert.True(Cell(grid, 0, "NAME").IsCurrent);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Shift and a click takes the block out to the cell that was pressed.</summary>
    [AvaloniaFact]
    public void ShiftAndAClickExtendsFromTheAnchor()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            var far = Cell(grid, 2, "KIND");

            far.RaiseEvent(Pressed(far, KeyModifiers.Shift));
            Dispatcher.UIThread.RunJobs();

            // Three rows by three columns, less the anchor.
            Assert.Equal(8, InRange(grid).Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Copy writes the block rather than the whole rows.</summary>
    [AvaloniaFact]
    public async Task CopyWritesTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);
            Press(grid, Key.C, KeyModifiers.Control);

            Dispatcher.UIThread.RunJobs();

            var text = window.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

            Assert.Equal("a0\tb0\r\na1\tb1", text);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A drag over the cells takes the block with it, which is how a person does it.</summary>
    [AvaloniaFact]
    public void ADragOverTheCellsMakesTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            // Through the window rather than at the cell, since the pointer is captured by
            // whatever was pressed and hit testing is what finds the cell under it.
            window.MouseDown(At(window, Cell(grid, 0, "ID")), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Over(window, Cell(grid, 0, "NAME"));
            Over(window, Cell(grid, 1, "NAME"));

            // Two rows by two columns, less the anchor.
            Assert.Equal(3, InRange(grid).Count);

            // The button comes up, so moving on afterwards leaves the block alone.
            window.MouseUp(At(window, Cell(grid, 1, "NAME")), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            window.MouseMove(At(window, Cell(grid, 2, "KIND")));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, InRange(grid).Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A drag in row units is the list's own and makes no block.</summary>
    [AvaloniaFact]
    public void ADragInRowUnitsMakesNoBlock()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            window.MouseDown(At(window, Cell(grid, 0, "ID")), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Over(window, Cell(grid, 1, "NAME"));

            Assert.Empty(InRange(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The footer swaps the row count for what the block holds.</summary>
    [AvaloniaFact]
    public void TheFooterCountsTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell, numbers: true);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            // With no block it is still the row count, which is what the design swaps out.
            Assert.Equal("1 selected", grid.SelectionText);

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);

            Assert.Equal("4 cells, sum 2.00, avg 0.50", grid.SelectionText);

            Press(grid, Key.Right);

            Assert.Equal("1 selected", grid.SelectionText);
        }
        finally
        {
            window.Close();
        }
    }

    private static List<DataGridCell> InRange(DataGrid grid) =>
        [.. grid.GetVisualDescendants().OfType<DataGridCell>().Where(cell => cell.IsInRange)];

    private static DataGridCell Cell(DataGrid grid, int row, string header) =>
        (grid.ContainerFromIndex(row) as Visual)!
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .Single(cell => (string?)cell.Column?.Header == header);

    /// <summary>The middle of a cell, in the window's own coordinates.</summary>
    private static Point At(Window window, Visual cell) =>
        cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), window)!.Value;

    /// <summary>The pointer travelling over a cell with the button still down.</summary>
    private static void Over(Window window, Visual cell)
    {
        window.MouseMove(At(window, cell), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = target,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static PointerPressedEventArgs Pressed(Control target, KeyModifiers modifiers) => new(
        target,
        new Pointer(0, PointerType.Mouse, true),
        target,
        default,
        0,
        new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
        modifiers);

    private static DataGrid Grid(
        out Window window,
        GridSelectionUnit unit = GridSelectionUnit.Row,
        bool numbers = false)
    {
        var grid = new DataGrid { SelectionUnit = unit };

        grid.Columns.Add(Column("ID", entry => entry.Id));
        grid.Columns.Add(Column("NAME", entry => entry.Name));
        grid.Columns.Add(Column("KIND", entry => entry.Kind));

        // The row's number in every column, so a block over the first two rows and the
        // first two columns holds zero, zero, one, one.
        grid.ItemsSource = new GridRows(
            Enumerable.Range(0, 3).Select(number => numbers
                ? new Entry($"{number}", $"{number}", $"{number}")
                : new Entry($"a{number}", $"b{number}", $"c{number}")).ToList());

        window = new Window { Content = grid, Width = 520, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header, Func<Entry, object?> value) => new()
    {
        Header = header,
        Width = new GridLength(1, GridUnitType.Star),
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : value(entry)?.ToString() }),
        Value = item => value((Entry)item),
    };

    private sealed record Entry(string Id, string Name, string Kind);
}
