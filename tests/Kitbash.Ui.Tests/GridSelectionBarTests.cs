using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The bar a grid floats over its last rows once rows are picked. What is under test is when
/// it is up, that the count is printed once rather than twice, and the three ways it goes
/// away again.
/// </summary>
public sealed class GridSelectionBarTests
{
    /// <summary>A grid that has nothing to do with a set never grows a bar.</summary>
    [AvaloniaFact]
    public void NoActionsMeansNoBar()
    {
        var grid = Grid(out var window, actions: false);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(string.Empty, grid.PickedText);
            Assert.False(Bar(grid).IsVisible);

            // And the footer still says it, since nothing else is going to.
            Assert.Equal("1 selected", grid.SelectionText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Picking a row raises the bar, and unpicking every row drops it.</summary>
    [AvaloniaFact]
    public void ThePickedSetRaisesTheBar()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.False(Bar(grid).IsVisible);

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.True(Bar(grid).IsVisible);
            Assert.Equal("1 selected", grid.PickedText);

            grid.SelectedItems!.Add(grid.ItemsView[2]);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("2 selected", grid.PickedText);

            grid.UnselectAll();
            Dispatcher.UIThread.RunJobs();

            Assert.False(Bar(grid).IsVisible);
            Assert.Equal(string.Empty, grid.PickedText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The footer gives the count back to the query while the bar has it, so the same number
    /// is never printed twice.
    /// </summary>
    [AvaloniaFact]
    public void TheCountIsPrintedOnce()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("1 selected", grid.PickedText);
            Assert.Equal(string.Empty, grid.SelectionText);

            grid.SelectionActions = null;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(string.Empty, grid.PickedText);
            Assert.Equal("1 selected", grid.SelectionText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A live block is a different number, so the footer keeps saying what the block holds
    /// while the bar says how many rows are picked.
    /// </summary>
    [AvaloniaFact]
    public void ABlockStillSpeaksInTheFooter()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectionUnit = GridSelectionUnit.Cell;
            grid.SelectedIndex = 0;
            grid.Focus();
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("1 selected", grid.PickedText);
            Assert.NotEqual(string.Empty, grid.SelectionText);
            Assert.DoesNotContain("selected", grid.SelectionText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The bar's own button drops the selection.</summary>
    [AvaloniaFact]
    public void TheBarDropsTheSelection()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            var drop = Bar(grid)
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "PART_Drop");

            drop.Command = null;
            drop.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(-1, grid.SelectedIndex);
            Assert.False(Bar(grid).IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Escape drops the selection while the bar owns it.</summary>
    [AvaloniaFact]
    public void EscapeDropsTheSelection()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 1;
            grid.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.True(Escape(grid));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(-1, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// And nowhere else. A grid inside a dialog has to leave Escape to the dialog, so a grid
    /// with no bar never takes it.
    /// </summary>
    [AvaloniaFact]
    public void EscapeIsLeftAloneWithoutABar()
    {
        var grid = Grid(out var window, actions: false);

        try
        {
            grid.SelectedIndex = 1;
            grid.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.False(Escape(grid));
            Assert.Equal(1, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An action in the bar is a compact button, since the bar is one strip of chrome.</summary>
    [AvaloniaFact]
    public void AnActionInTheBarIsCompact()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var action = Bar(grid)
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name != "PART_Drop");

            Assert.Same(window.FindResource("CompactButton"), action.Theme);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// It floats over the last rows rather than pushing them, so nothing reflows when a
    /// selection starts. Measured as the rows keeping their places and the bar landing on
    /// top of the last one.
    /// </summary>
    [AvaloniaFact]
    public void TheBarSitsOnTheRowsRatherThanMovingThem()
    {
        var grid = Grid(out var window);

        try
        {
            var before = Rows(grid);

            Assert.NotEmpty(before);

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(before, Rows(grid));

            var bar = Bar(grid);
            var top = bar.TranslatePoint(new Point(0, 0), grid)!.Value.Y;
            var last = grid.GetVisualDescendants().OfType<DataGridRow>().Last();
            var reach = last.TranslatePoint(new Point(0, 0), grid)!.Value;

            Assert.True(bar.Bounds.Height > 0, "the bar has a size once it is up");
            Assert.True(top + bar.Bounds.Height > reach.Y, "and it lands over the last row rather than under it");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Where every realised row sits, so a reflow shows up as a different list.</summary>
    private static List<double> Rows(DataGrid grid) =>
    [
        .. grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Select(row => row.TranslatePoint(new Point(0, 0), grid)?.Y ?? double.NaN),
    ];

    private static void Press(DataGrid grid, Key key, KeyModifiers modifiers) =>
        grid.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        });

    private static bool Escape(DataGrid grid)
    {
        var key = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Escape,
        };

        grid.RaiseEvent(key);

        return key.Handled;
    }

    private static GridSelectionBar Bar(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<GridSelectionBar>().Single();

    private static DataGrid Grid(out Window window, bool actions = true)
    {
        var grid = new DataGrid { SelectionMode = SelectionMode.Multiple };

        grid.Columns.Add(Column("ID", entry => entry.Id));
        grid.Columns.Add(Column("NAME", entry => entry.Name));

        grid.ItemsSource = new GridRows(
            Enumerable.Range(0, 6).Select(number => new Entry($"a{number}", $"b{number}")).ToList());

        if (actions)
        {
            grid.SelectionActions = new Button { Content = "Delete", Classes = { "danger" } };
        }

        window = new Window { Content = grid, Width = 520, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header, Func<Entry, string> read) => new()
    {
        Header = header,
        Width = new GridLength(1, GridUnitType.Star),
        Value = item => item is Entry entry ? read(entry) : null,
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : read(entry) }),
    };

    private sealed record Entry(string Id, string Name);
}
