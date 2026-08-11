using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A group heading is a heading, not a row of data. The pointer already knew that. The
/// keyboard and select all did not, so both counted headings as picked rows.
/// </summary>
public sealed class GridGroupSelectionTests
{
    [AvaloniaFact]
    public void SelectAllTakesTheRowsAndNotTheHeadings()
    {
        var (window, grid) = Grouped();

        try
        {
            grid.SelectAll();
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(grid.SelectedItems!.Cast<GridRow>(), row => row.IsGroup);
            Assert.Equal(4, grid.SelectedItems!.Count);
            Assert.Equal("4 selected", grid.SelectionText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Arrowing down off the last row of a group steps over the next heading and onto the
    /// first row under it, rather than stopping on the heading.
    /// </summary>
    [AvaloniaFact]
    public void ArrowingStepsOverAHeading()
    {
        var (window, grid) = Grouped();

        try
        {
            // The last row of the first group. Row 0 is that group's heading.
            grid.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();

            grid.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Down,
                Source = grid,
            });

            Dispatcher.UIThread.RunJobs();

            var landed = Assert.IsType<GridRow>(grid.SelectedItem);

            Assert.False(landed.IsGroup);
            Assert.Equal("third", ((Entry)landed.Item!).Name);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Two groups of two, so the list is heading, row, row, heading, row, row.</summary>
    private static (Window Window, DataGrid Grid) Grouped()
    {
        var items = new[]
        {
            new Entry("first", "alpha"),
            new Entry("second", "alpha"),
            new Entry("third", "beta"),
            new Entry("fourth", "beta"),
        };

        var grid = new DataGrid { SelectionMode = SelectionMode.Multiple };

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        var rows = new GridRows(items);

        rows.Group(item => ((Entry)item).Group);
        grid.ItemsSource = rows;

        var window = new Window { Content = grid, Width = 420, Height = 300 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(6, grid.ItemCount);

        return (window, grid);
    }

    private sealed record Entry(string Name, string Group);
}
