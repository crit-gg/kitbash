using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The three empty states. All three keep the toolbar and the header on screen, since a grid
/// is a place rather than a page and what gets a person out of the state has to stay where
/// it was.
/// </summary>
public sealed class GridEmptyStateTests
{
    /// <summary>A grid that has never had anything says so, and it is the one that teaches.</summary>
    [AvaloniaFact]
    public void NothingYetIsTheOneThatTeaches()
    {
        var grid = Grid(out var window, rows: 0);

        try
        {
            Assert.Contains(":nothing", grid.Classes);
            Assert.DoesNotContain(":nomatches", grid.Classes);
            Assert.True(Shown(grid, "PART_Nothing"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid emptied by its filter is a different state, since only one teaches.</summary>
    [AvaloniaFact]
    public void AFilterThatEmptiesItSaysSomethingElse()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.DoesNotContain(":nothing", grid.Classes);

            grid.Rows!.Filter = _ => false;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(":nomatches", grid.Classes);
            Assert.DoesNotContain(":nothing", grid.Classes);
            Assert.True(Shown(grid, "PART_NoMatches"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Loading is skeleton rows and never a spinner.</summary>
    [AvaloniaFact]
    public void LoadingDrawsSkeletonRows()
    {
        var grid = Grid(out var window, rows: 0);

        try
        {
            grid.IsLoading = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(":loading", grid.Classes);

            // And it is not also saying there is nothing, since nothing is known yet.
            Assert.DoesNotContain(":nothing", grid.Classes);
            Assert.True(Shown(grid, "PART_Skeleton"));

            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<ProgressBar>(), bar => bar.IsIndeterminate);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The toolbar and the header stay through every one of them.</summary>
    [AvaloniaFact]
    public void TheToolbarAndTheHeaderStay()
    {
        var grid = Grid(out var window, rows: 0);

        try
        {
            grid.Toolbar = new TextBlock { Text = "toolbar" };
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            Assert.Contains(grid.GetVisualDescendants().OfType<DataGridHeader>(), header => header.IsVisible);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "toolbar");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid with rows says nothing at all.</summary>
    [AvaloniaFact]
    public void AGridWithRowsSaysNothing()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.DoesNotContain(":nothing", grid.Classes);
            Assert.DoesNotContain(":nomatches", grid.Classes);
            Assert.DoesNotContain(":loading", grid.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    private static bool Shown(DataGrid grid, string part) =>
        grid.GetVisualDescendants()
            .OfType<Control>()
            .Any(control => control.Name == part && control.IsVisible);

    private static DataGrid Grid(out Window window, int rows = 3)
    {
        var grid = new DataGrid
        {
            EmptyContent = new TextBlock { Text = "Nothing here yet." },
            NoMatchesContent = new TextBlock { Text = "No matches." },
        };

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = new GridRows(
            Enumerable.Range(0, rows).Select(number => new Entry($"r{number}")).ToList());

        window = new Window { Content = grid, Width = 420, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Name);
}
