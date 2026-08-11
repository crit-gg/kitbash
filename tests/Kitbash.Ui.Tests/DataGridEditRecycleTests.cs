using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Editing a cell and then scrolling away from it. The container is handed to another row,
/// so an editor left open would be drawn over data it does not belong to.
/// </summary>
public sealed class DataGridEditRecycleTests
{
    [AvaloniaFact]
    public void ScrollingAwayFromAnOpenEditorEndsTheEdit()
    {
        var items = Enumerable.Range(0, 400).Select(number => new Row { Name = $"row {number}" }).ToArray();
        var (window, grid) = Shown(items);

        try
        {
            var cell = grid.GetVisualDescendants().OfType<DataGridCell>().First();
            var edited = (Row)cell.Content!;

            grid.BeginEdit(cell);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(cell, grid.EditingCell);
            Assert.Equal(1, edited.Begun);

            var box = Assert.Single(cell.GetVisualDescendants().OfType<TextBox>());

            box.Text = "typed";
            Scroll(grid, 6000);

            // The grid is no longer holding a cell that has been handed to another row.
            Assert.Null(grid.EditingCell);
            Assert.Equal(1, edited.Ended);
            Assert.Equal(0, edited.Cancelled);

            // And nothing on screen is still showing an editor.
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<DataGridCell>(), cell => cell.IsEditing);
            Assert.Empty(grid.GetVisualDescendants().OfType<TextBox>());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The row the container went on to stand for draws its own value, rather than the
    /// one that was being typed into.
    /// </summary>
    [AvaloniaFact]
    public void TheRecycledRowShowsItsOwnData()
    {
        var items = Enumerable.Range(0, 400).Select(number => new Row { Name = $"row {number}" }).ToArray();
        var (window, grid) = Shown(items);

        try
        {
            var cell = grid.GetVisualDescendants().OfType<DataGridCell>().First();

            grid.BeginEdit(cell);
            Dispatcher.UIThread.RunJobs();

            Scroll(grid, 6000);

            var shown = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Select(cell => cell.Content)
                .OfType<Row>()
                .Select(row => row.Name)
                .ToList();

            Assert.NotEmpty(shown);
            Assert.DoesNotContain("row 0", shown);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Scroll(DataGrid grid, double to)
    {
        // By templated parent, since a TextBox in an open editor carries a part of the
        // same name inside its own template.
        var scroller = Assert.Single(
            grid.GetVisualDescendants().OfType<ScrollViewer>(),
            viewer => ReferenceEquals(viewer.TemplatedParent, grid));

        scroller.Offset = new Vector(0, to);

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Window Window, DataGrid Grid) Shown(IEnumerable<Row> items)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Row>((row, _) => new TextBlock { Text = row?.Name }),
            EditTemplate = new FuncDataTemplate<Row>((row, _) => new TextBox { Text = row?.Name }),
        });

        grid.ItemsSource = new GridRows(items);

        var window = new Window { Content = grid, Width = 420, Height = 300 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return (window, grid);
    }

    private sealed class Row : IEditableObject
    {
        public string Name { get; set; } = string.Empty;

        public int Begun { get; private set; }

        public int Ended { get; private set; }

        public int Cancelled { get; private set; }

        public void BeginEdit() => Begun++;

        public void CancelEdit() => Cancelled++;

        public void EndEdit() => Ended++;
    }
}
