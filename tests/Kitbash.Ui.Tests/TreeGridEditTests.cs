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
/// The tree grid edits a cell. It could not before the edit state moved onto the piece both
/// grids share, so a double click in a cell only ever opened or closed the row.
/// </summary>
public sealed class TreeGridEditTests
{
    /// <summary>A double click in a cell that can be edited opens the editor.</summary>
    [AvaloniaFact]
    public void ADoubleClickInACellEditsRatherThanOpensTheRow()
    {
        var roots = new[] { new Entry("alpha", new Entry("a1")) };
        var grid = Grid(roots, out var window);

        try
        {
            var rows = (TreeRows)grid.ItemsSource!;
            var cell = Cells(grid).First();

            DoubleClick(cell);

            Assert.Same(cell, grid.EditingCell);
            Assert.True(cell.IsEditing);

            // The row it happened in is still closed, so the gesture went to the cell
            // rather than to the hierarchy.
            Assert.False(rows[0].IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A double click on a cell that cannot be edited still opens the row.</summary>
    [AvaloniaFact]
    public void ADoubleClickInAPlainCellStillOpensTheRow()
    {
        var roots = new[] { new Entry("alpha", new Entry("a1")) };
        var grid = Grid(roots, out var window, editable: false);

        try
        {
            var rows = (TreeRows)grid.ItemsSource!;

            DoubleClick(Cells(grid).First());
            Dispatcher.UIThread.RunJobs();

            Assert.Null(grid.EditingCell);
            Assert.True(rows[0].IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Escape in the editor puts the old value back, the way it does in the flat grid.</summary>
    [AvaloniaFact]
    public void EscapeCancelsTheEdit()
    {
        var grid = Grid([new Entry("alpha")], out var window);

        try
        {
            var cell = Cells(grid).First();

            DoubleClick(cell);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(cell, grid.EditingCell);

            cell.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
                Source = cell,
            });

            Assert.Null(grid.EditingCell);
            Assert.False(cell.IsEditing);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The hierarchy keeps left and right in the column that draws the caret, and the cells
    /// take them everywhere else.
    /// </summary>
    [AvaloniaFact]
    public void TheCaretColumnKeepsLeftAndRight()
    {
        var grid = Grid([new Entry("alpha", new Entry("a1"))], out var window, columns: 2);

        try
        {
            var rows = (TreeRows)grid.ItemsSource!;

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            // In the caret column, right opens the row rather than moving the mark.
            Press(grid, Key.Right);

            Assert.True(rows[0].IsExpanded);
            Assert.Equal("NAME", Current(grid)?.Column?.Header);

            // Tab moves the mark out of it, and now right moves a column instead.
            Press(grid, Key.Tab);

            Assert.Equal("KIND", Current(grid)?.Column?.Header);

            Press(grid, Key.Left);

            Assert.Equal("NAME", Current(grid)?.Column?.Header);
            Assert.True(rows[0].IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGridCell? Current(TreeDataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault(cell => cell.IsCurrent);

    private static void Press(Control target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            Source = target,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<DataGridCell> Cells(TreeDataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().Where(cell => cell.IsVisible);

    private static TreeDataGrid Grid(Entry[] roots, out Window window, bool editable = true, int columns = 1)
    {
        var grid = new TreeDataGrid();

        var column = new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        };

        if (editable)
        {
            column.EditTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBox { Text = entry?.Name });
        }

        grid.Columns.Add(column);

        for (var extra = 1; extra < columns; extra++)
        {
            grid.Columns.Add(new GridColumn
            {
                Header = "KIND",
                Width = new GridLength(1, GridUnitType.Star),
                CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            });
        }
        grid.ItemsSource = new TreeRows(roots, item => ((Entry)item).Children);

        window = new Window { Content = grid, Width = 420, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static void DoubleClick(Control target) =>
        target.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, new PointerReleasedEventArgs(
            target,
            new Pointer(0, PointerType.Mouse, true),
            target,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left)));

    private sealed class Entry(string name, params Entry[] children)
    {
        public string Name => name;

        public Entry[] Children => children;
    }
}
