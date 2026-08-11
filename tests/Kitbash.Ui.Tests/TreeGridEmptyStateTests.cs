using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The tree grid's three empty states. It tells an empty tree from an emptied one by whether
/// a filter is on, since a filter is the only thing that takes rows away without the source
/// changing.
/// </summary>
public sealed class TreeGridEmptyStateTests
{
    /// <summary>A tree that has never had anything says so, and it is the one that teaches.</summary>
    [AvaloniaFact]
    public void NothingYetIsTheOneThatTeaches()
    {
        var grid = Grid(out var window, roots: 0);

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

    /// <summary>A tree emptied by its filter is a different state.</summary>
    [AvaloniaFact]
    public void AFilterThatEmptiesItSaysSomethingElse()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.DoesNotContain(":nothing", grid.Classes);

            Rows(grid).Filter(_ => false);
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

    /// <summary>And clearing the filter puts the rows and the state back.</summary>
    [AvaloniaFact]
    public void ClearingTheFilterPutsTheStateBack()
    {
        var grid = Grid(out var window);

        try
        {
            Rows(grid).Filter(_ => false);
            Dispatcher.UIThread.RunJobs();

            Rows(grid).Filter(null);
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(":nomatches", grid.Classes);
            Assert.DoesNotContain(":nothing", grid.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A filter that keeps something is not an empty state, however deep the match was and
    /// however many branches were opened to reach it.
    /// </summary>
    [AvaloniaFact]
    public void AFilterThatKeepsSomethingIsNotEmpty()
    {
        var grid = Grid(out var window);

        try
        {
            Rows(grid).Filter(item => ((Node)item).Name == "leaf");
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(":nomatches", grid.Classes);
            Assert.DoesNotContain(":nothing", grid.Classes);
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
        var grid = Grid(out var window, roots: 0);

        try
        {
            grid.IsLoading = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(":loading", grid.Classes);
            Assert.DoesNotContain(":nothing", grid.Classes);
            Assert.True(Shown(grid, "PART_Skeleton"));

            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<ProgressBar>(), bar => bar.IsIndeterminate);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The header stays through every one of them.</summary>
    [AvaloniaFact]
    public void TheHeaderStays()
    {
        var grid = Grid(out var window, roots: 0);

        try
        {
            Assert.Contains(grid.GetVisualDescendants().OfType<DataGridHeader>(), header => header.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Collapsing the last open branch is not an empty state, since the roots remain.</summary>
    [AvaloniaFact]
    public void ClosingABranchIsNotAnEmptyState()
    {
        var grid = Grid(out var window);

        try
        {
            var rows = Rows(grid);

            rows.Expand(rows[0]);
            Dispatcher.UIThread.RunJobs();
            rows.Collapse(rows[0]);
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(":nothing", grid.Classes);
            Assert.DoesNotContain(":nomatches", grid.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    private static TreeRows Rows(TreeDataGrid grid) => (TreeRows)grid.ItemsSource!;

    private static bool Shown(TreeDataGrid grid, string part) =>
        grid.GetVisualDescendants()
            .OfType<Control>()
            .Any(control => control.Name == part && control.IsVisible);

    private static TreeDataGrid Grid(out Window window, int roots = 2)
    {
        var grid = new TreeDataGrid
        {
            EmptyContent = new TextBlock { Text = "Nothing here yet." },
            NoMatchesContent = new TextBlock { Text = "No matches." },
        };

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Node>((node, _) => new TextBlock { Text = node?.Name }),
        });

        var trees = Enumerable
            .Range(0, roots)
            .Select(number => new Node($"root{number}", new Node("leaf")))
            .ToArray();

        grid.ItemsSource = new TreeRows(trees, item => ((Node)item).Children);

        window = new Window { Content = grid, Width = 420, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed class Node(string name, params Node[] children)
    {
        public string Name => name;

        public Node[] Children => children;
    }
}
