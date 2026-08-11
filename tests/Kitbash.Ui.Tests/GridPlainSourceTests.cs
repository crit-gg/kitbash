using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A grid handed an ordinary list rather than the rows it wants. It used to draw the right
/// number of rows with every cell empty and nothing in the log, which is the worst way for
/// a rule to be enforced.
/// </summary>
public sealed class GridPlainSourceTests
{
    [AvaloniaFact]
    public void APlainListIsWrappedRatherThanDrawnBlank()
    {
        var items = new[] { new Entry("first"), new Entry("second") };
        var (window, grid) = Shown(grid => grid.ItemsSource = items);

        try
        {
            Assert.NotNull(grid.Rows);
            Assert.Equal(2, grid.Rows!.Count);

            Assert.Equal(["first", "second"], Drawn(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// And through a binding, since a view names its list rather than assigning one. The
    /// wrap must not fight the binding for the property.
    /// </summary>
    [AvaloniaFact]
    public void ABoundPlainListIsWrappedToo()
    {
        var source = new ObservableCollection<Entry> { new("bound first"), new("bound second") };
        var (window, grid) = Shown(grid => grid.Bind(
            DataGrid.ItemsSourceProperty,
            new Binding(".") { Source = source }));

        try
        {
            Assert.NotNull(grid.Rows);
            Assert.Equal(["bound first", "bound second"], Drawn(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The rows a caller built are used as they are, not wrapped a second time.</summary>
    [AvaloniaFact]
    public void RowsAreLeftAlone()
    {
        var rows = new GridRows(new[] { new Entry("only") });
        var (window, grid) = Shown(grid => grid.ItemsSource = rows);

        try
        {
            Assert.Same(rows, grid.Rows);
            Assert.Same(rows, grid.ItemsSource);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A tree given a flat list is the same failure, so it takes the same answer.</summary>
    [AvaloniaFact]
    public void ATreeTakesAPlainListAsAFlatOne()
    {
        var items = new[] { new Entry("leaf"), new Entry("another leaf") };
        var tree = new Tree
        {
            ItemTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            ItemsSource = items,
        };

        var window = new Window { Content = tree, Width = 420, Height = 220 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        try
        {
            Assert.Equal(
                ["leaf", "another leaf"],
                tree.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<string?> Drawn(DataGrid grid) =>
        grid.GetVisualDescendants()
            .OfType<DataGridCell>()
            .Select(cell => cell.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text);

    private static (Window Window, DataGrid Grid) Shown(Action<DataGrid> fill)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        fill(grid);

        var window = new Window { Content = grid, Width = 420, Height = 220 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return (window, grid);
    }

    private sealed record Entry(string Name);
}
