using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Attaching a pager, and turning a page. The pager used to write its own default over a
/// page size the caller had already set, and a turn left the body wherever it had been
/// scrolled to.
/// </summary>
public sealed class GridPagerTests
{
    /// <summary>A page size already on the rows is what the pager takes up.</summary>
    [AvaloniaFact]
    public void ThePagerAdoptsThePageSizeTheRowsAlreadyHad()
    {
        var rows = new GridRows(Items(500)) { PageSize = 25 };
        var pager = new GridPager { Rows = rows };

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(25, rows.PageSize);
        Assert.Equal(25, pager.PageSize);
        Assert.Equal(20, rows.PageCount);
    }

    /// <summary>Rows that have never been paged take the pager's own.</summary>
    [AvaloniaFact]
    public void RowsWithNoPageSizeTakeThePagers()
    {
        var rows = new GridRows(Items(500));
        var pager = new GridPager { PageSize = 50, Rows = rows };

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(50, rows.PageSize);
        Assert.Equal(10, rows.PageCount);
    }

    /// <summary>Turning a page starts at the top of it.</summary>
    [AvaloniaFact]
    public void TurningAPageGoesBackToTheFirstRow()
    {
        var rows = new GridRows(Items(500)) { PageSize = 100 };
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = rows;

        var window = new Window { Content = grid, Width = 420, Height = 300 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var scroller = Assert.Single(
                grid.GetVisualDescendants().OfType<ScrollViewer>(),
                viewer => ReferenceEquals(viewer.TemplatedParent, grid));

            scroller.Offset = new Vector(0, 900);
            Dispatcher.UIThread.RunJobs();

            Assert.True(scroller.Offset.Y > 0);

            rows.Page = 2;
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, scroller.Offset.Y);
        }
        finally
        {
            window.Close();
        }
    }

    private static Entry[] Items(int count) =>
        [.. Enumerable.Range(0, count).Select(number => new Entry($"row {number}"))];

    private sealed record Entry(string Name);
}
