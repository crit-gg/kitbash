using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The current cell. There was none at all before this, so no key moved across a row and
/// nothing said where the keyboard was.
/// </summary>
public sealed class GridCurrentCellTests
{
    /// <summary>Right moves the mark one column and left brings it back.</summary>
    [AvaloniaFact]
    public void LeftAndRightMoveTheMarkAcrossTheRow()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("ID", Current(grid)?.Column?.Header);

            Press(grid, Key.Right);
            Assert.Equal("NAME", Current(grid)?.Column?.Header);

            Press(grid, Key.Right);
            Assert.Equal("KIND", Current(grid)?.Column?.Header);

            Press(grid, Key.Left);
            Assert.Equal("NAME", Current(grid)?.Column?.Header);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The mark stops at either edge rather than wrapping.</summary>
    [AvaloniaFact]
    public void TheMarkStopsAtTheEdges()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Left);
            Assert.Equal("ID", Current(grid)?.Column?.Header);

            Press(grid, Key.End);
            Assert.Equal("KIND", Current(grid)?.Column?.Header);

            Press(grid, Key.Right);
            Assert.Equal("KIND", Current(grid)?.Column?.Header);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Home and End go to the ends of the row, not the ends of the list.</summary>
    [AvaloniaFact]
    public void HomeAndEndAreTheEndsOfTheRow()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.End);

            Assert.Equal("KIND", Current(grid)?.Column?.Header);
            Assert.Equal(1, grid.SelectedIndex);

            Press(grid, Key.Home);

            Assert.Equal("ID", Current(grid)?.Column?.Header);
            Assert.Equal(1, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Control and Home go to the first cell of the first row.</summary>
    [AvaloniaFact]
    public void ControlHomeAndEndGoToTheCornersOfTheGrid()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.End, KeyModifiers.Control);

            Assert.Equal("KIND", Current(grid)?.Column?.Header);
            Assert.Equal(2, grid.SelectedIndex);

            Press(grid, Key.Home, KeyModifiers.Control);

            Assert.Equal("ID", Current(grid)?.Column?.Header);
            Assert.Equal(0, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Tab walks the cells and wraps to the next row at the end of one.</summary>
    [AvaloniaFact]
    public void TabWrapsToTheNextRow()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.End);
            Press(grid, Key.Tab);

            Assert.Equal(1, grid.SelectedIndex);
            Assert.Equal("ID", Current(grid)?.Column?.Header);

            Press(grid, Key.Tab, KeyModifiers.Shift);

            Assert.Equal(0, grid.SelectedIndex);
            Assert.Equal("KIND", Current(grid)?.Column?.Header);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Moving the selection keeps the column the keyboard was in.</summary>
    [AvaloniaFact]
    public void MovingRowKeepsTheColumn()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right);
            Assert.Equal("NAME", Current(grid)?.Column?.Header);

            grid.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("NAME", Current(grid)?.Column?.Header);
            Assert.Same(grid.ContainerFromIndex(2), Current(grid)?.FindAncestorOfType<DataGridRow>());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Only ever one cell carries the mark.</summary>
    [AvaloniaFact]
    public void OneCellCarriesTheMark()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right);

            Assert.Single(grid.GetVisualDescendants().OfType<DataGridCell>(), cell => cell.IsCurrent);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>F2 opens the current cell, and only where the column allows it.</summary>
    [AvaloniaFact]
    public void TwoOpensTheCurrentCell()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            // The ID column carries no edit template, so F2 there does nothing at all.
            Press(grid, Key.F2);
            Assert.Null(grid.EditingCell);

            Press(grid, Key.Right);
            Press(grid, Key.F2);

            Assert.NotNull(grid.EditingCell);
            Assert.Equal("NAME", grid.EditingCell!.Column?.Header);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told to take no gesture opens nothing.</summary>
    [AvaloniaFact]
    public void NoGesturesIsTheReadOnlyGrid()
    {
        var grid = Grid(out var window);

        try
        {
            grid.BeginEditGestures = BeginEditGestures.None;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right);
            Press(grid, Key.F2);

            Assert.Null(grid.EditingCell);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGridCell? Current(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault(cell => cell.IsCurrent);

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

    private static DataGrid Grid(out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", entry => entry.Id, editable: false));
        grid.Columns.Add(Column("NAME", entry => entry.Name, editable: true));
        grid.Columns.Add(Column("KIND", entry => entry.Kind, editable: true));

        grid.ItemsSource = new GridRows(new[]
        {
            new Entry("one", "first", "table"),
            new Entry("two", "second", "graph"),
            new Entry("three", "third", "sheet"),
        });

        window = new Window { Content = grid, Width = 520, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header, Func<Entry, string> read, bool editable)
    {
        var column = new GridColumn
        {
            Header = header,
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
                new TextBlock { Text = entry is null ? null : read(entry) }),
        };

        if (editable)
        {
            column.EditTemplate = new FuncDataTemplate<Entry>((entry, _) =>
                new TextBox { Text = entry is null ? null : read(entry) });
        }

        return column;
    }

    private sealed record Entry(string Id, string Name, string Kind);
}
