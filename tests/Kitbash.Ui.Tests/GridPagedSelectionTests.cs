using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a selection means once a grid pages. The selection model can only hold rows the list
/// has, so a page turn used to forget everything picked, which the action bar made into a
/// silent hole: pick five rows, turn the page, and a set wide action is talking about nothing.
/// </summary>
public sealed class GridPagedSelectionTests
{
    /// <summary>A page turn keeps what was picked on the page it left.</summary>
    [AvaloniaFact]
    public void APageTurnKeepsWhatWasPicked()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            grid.SelectedItems!.Add(grid.ItemsView[1]);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, grid.PickedItems.Count);

            grid.Rows!.Page = 2;
            Dispatcher.UIThread.RunJobs();

            // Gone from the list, since the list is another page now, and still picked.
            Assert.Empty(grid.SelectedItems!);
            Assert.Equal(2, grid.PickedItems.Count);
            Assert.Contains(items[0], grid.PickedItems);
            Assert.Contains(items[1], grid.PickedItems);
            Assert.Equal("2 selected", grid.PickedText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>And coming back picks them again where they are.</summary>
    [AvaloniaFact]
    public void ComingBackPicksThemAgain()
    {
        var grid = Grid(out var window, out _);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.Rows!.Page = 2;
            Dispatcher.UIThread.RunJobs();

            grid.Rows.Page = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Single(grid.SelectedItems!);
            Assert.Equal(0, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Picking on a second page adds to the set rather than replacing it.</summary>
    [AvaloniaFact]
    public void TwoPagesAddUp()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.Rows!.Page = 2;
            Dispatcher.UIThread.RunJobs();

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, grid.PickedItems.Count);
            Assert.Contains(items[0], grid.PickedItems);
            Assert.Contains(items[4], grid.PickedItems);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Unpicking a row on the page takes it out of the set for good.</summary>
    [AvaloniaFact]
    public void UnpickingTakesItOut()
    {
        var grid = Grid(out var window, out _);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.SelectedItems!.Clear();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(grid.PickedItems);
            Assert.Equal(string.Empty, grid.PickedText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A filter that hides a picked row unpicks it, so a set wide action never reaches
    /// something a person cannot see.
    /// </summary>
    [AvaloniaFact]
    public void AFilterUnpicksWhatItHides()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            grid.SelectedItems!.Add(grid.ItemsView[1]);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, grid.PickedItems.Count);

            grid.Rows!.Filter = item => !ReferenceEquals(item, items[0]);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(grid.PickedItems);
            Assert.Contains(items[1], grid.PickedItems);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An item the source drops is dropped from the set too.</summary>
    [AvaloniaFact]
    public void AnItemThatLeavesTheSourceLeavesTheSet()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.ItemsSource = new GridRows(items.Skip(1).ToList()) { PageSize = 4 };
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(items[0], grid.PickedItems);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Sorting keeps every pick, on the page and off it.</summary>
    [AvaloniaFact]
    public void SortingKeepsThemAll()
    {
        var grid = Grid(out var window, out _);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.Rows!.Page = 2;
            Dispatcher.UIThread.RunJobs();

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.Rows.Sort(grid.Columns[0], GridSortDirection.Descending);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, grid.PickedItems.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The bar's own way out drops the whole set, not only the page.</summary>
    [AvaloniaFact]
    public void ClearingDropsEveryPage()
    {
        var grid = Grid(out var window, out _);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            grid.Rows!.Page = 2;
            Dispatcher.UIThread.RunJobs();

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, grid.PickedItems.Count);

            grid.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Escape,
            });

            Dispatcher.UIThread.RunJobs();

            Assert.Empty(grid.PickedItems);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGrid Grid(out Window window, out Entry[] items)
    {
        items = [.. Enumerable.Range(0, 8).Select(number => new Entry($"a{number}"))];

        var grid = new DataGrid
        {
            SelectionMode = SelectionMode.Multiple,
            SelectionActions = new Button { Content = "Delete" },
        };

        grid.Columns.Add(new GridColumn
        {
            Header = "ID",
            Width = new GridLength(1, GridUnitType.Star),
            SortKey = item => ((Entry)item).Id,
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Id }),
        });

        grid.ItemsSource = new GridRows(items) { PageSize = 4 };

        window = new Window { Content = grid, Width = 420, Height = 260 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Id);
}
