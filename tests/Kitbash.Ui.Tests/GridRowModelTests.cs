using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The row model: identity across a rebuild, a rebuild that splices rather than resets,
/// the source being watched, filtering, and a sort on more than one column.
/// </summary>
public sealed class GridRowModelTests
{
    /// <summary>A row stands for one item and stays the same object when the list is built again.</summary>
    [AvaloniaFact]
    public void ARowIsTheSameObjectAfterASort()
    {
        var items = Items("pear", "apple", "fig");
        var rows = new GridRows(items);
        var column = Named();

        var before = rows[0];

        rows.Sort(column, GridSortDirection.Ascending);

        var after = rows.Single(row => ReferenceEquals(row.Item, items[0]));

        Assert.Same(before, after);
    }

    /// <summary>Rows picked before a sort are still picked after it.</summary>
    [AvaloniaFact]
    public void SortingKeepsTheSelection()
    {
        var items = Items("pear", "apple", "fig", "date");
        var rows = new GridRows(items);
        var grid = Grid(rows, out var window);

        try
        {
            grid.SelectionMode = SelectionMode.Multiple;
            grid.SelectedItems!.Add(rows.Single(row => ReferenceEquals(row.Item, items[0])));
            grid.SelectedItems!.Add(rows.Single(row => ReferenceEquals(row.Item, items[2])));

            Dispatcher.UIThread.RunJobs();

            rows.Sort(Named(), GridSortDirection.Ascending);
            Dispatcher.UIThread.RunJobs();

            var picked = grid.SelectedItems!.Cast<GridRow>().Select(row => row.Item).ToList();

            Assert.Equal(2, picked.Count);
            Assert.Contains(items[0], picked);
            Assert.Contains(items[2], picked);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Closing a group takes its rows out rather than throwing the list away.</summary>
    [AvaloniaFact]
    public void ClosingAGroupSplicesRatherThanResets()
    {
        var rows = new GridRows(Items("apple", "avocado", "fig", "date"));

        rows.Group(item => ((Entry)item).Name[..1]);

        var actions = new List<NotifyCollectionChangedAction>();

        rows.CollectionChanged += (_, e) => actions.Add(e.Action);
        rows.Toggle(rows.First(row => row.IsGroup));

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.NotEmpty(actions);
    }

    /// <summary>An item added to a source that notifies reaches the grid on its own.</summary>
    [AvaloniaFact]
    public void AnAddedItemArrivesWithNobodyCallingRefresh()
    {
        var items = new ObservableCollection<Entry> { new("pear"), new("apple") };
        var rows = new GridRows(items);

        items.Add(new Entry("fig"));

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows.Total);
    }

    /// <summary>A removed item leaves, and the rows around it are not made again.</summary>
    [AvaloniaFact]
    public void ARemovedItemLeavesAndTheRestStay()
    {
        var items = new ObservableCollection<Entry> { new("pear"), new("apple"), new("fig") };
        var rows = new GridRows(items);
        var kept = rows[2];

        items.RemoveAt(0);

        Assert.Equal(2, rows.Count);
        Assert.Same(kept, rows[1]);
    }

    /// <summary>A filter takes rows out and the counts still say how many there are.</summary>
    [AvaloniaFact]
    public void AFilterHidesRowsAndTheCountsStayHonest()
    {
        var rows = new GridRows(Items("pear", "apple", "fig", "avocado"))
        {
            Filter = item => ((Entry)item).Name.StartsWith('a'),
        };

        Assert.Equal(2, rows.Count);
        Assert.Equal(4, rows.Total);
        Assert.Equal(2, rows.Matched);
        Assert.Equal(2, rows.Shown);
    }

    /// <summary>A second column decides the rows the first one leaves tied.</summary>
    [AvaloniaFact]
    public void ASecondSortColumnBreaksTheTie()
    {
        var rows = new GridRows(Items(("b", 2), ("a", 2), ("c", 1)));

        var kind = new GridColumn { Header = "KIND", SortKey = item => ((Entry)item).Rank };
        var name = new GridColumn { Header = "NAME", SortKey = item => ((Entry)item).Name };

        rows.Sort(kind, GridSortDirection.Ascending);
        rows.AddSort(name, GridSortDirection.Ascending);

        Assert.Equal(["c", "a", "b"], rows.Select(row => ((Entry)row.Item!).Name));
        Assert.Equal(2, rows.Sorts.Count);
    }

    /// <summary>A group stays closed when its key is a fresh object each pass.</summary>
    [AvaloniaFact]
    public void AGroupStaysClosedWhenItsKeyIsRebuilt()
    {
        var rows = new GridRows(Items("apple", "avocado", "fig"));

        rows.Group(item => new Letter(((Entry)item).Name[..1]), new LetterKeys());
        rows.Toggle(rows.First(row => row.IsGroup));

        var closed = rows.First(row => row.IsGroup);

        Assert.False(closed.IsExpanded);

        rows.Refresh();

        Assert.False(rows.First(row => row.IsGroup).IsExpanded);
    }

    /// <summary>A sort leaves the body where it was rather than jumping to the top.</summary>
    [AvaloniaFact]
    public void TheScrollPositionSurvivesASort()
    {
        var rows = new GridRows(Items([.. Enumerable.Range(0, 300).Select(number => $"row {number:D3}")]));
        var grid = Grid(rows, out var window);

        try
        {
            var scroller = Assert.Single(
                grid.GetVisualDescendants().OfType<ScrollViewer>(),
                viewer => ReferenceEquals(viewer.TemplatedParent, grid));

            scroller.Offset = new Vector(0, 600);
            Dispatcher.UIThread.RunJobs();

            var was = scroller.Offset.Y;

            Assert.True(was > 0);

            rows.Sort(Named(), GridSortDirection.Descending);
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(was, scroller.Offset.Y);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An item arriving from the source does not unpick what was already picked.</summary>
    [AvaloniaFact]
    public void SelectionSurvivesAnItemArriving()
    {
        var items = new ObservableCollection<Entry> { new("pear"), new("apple") };
        var rows = new GridRows(items);
        var grid = Grid(rows, out var window);

        try
        {
            grid.SelectedItem = rows[1];
            Dispatcher.UIThread.RunJobs();

            items.Add(new Entry("fig"));
            Dispatcher.UIThread.RunJobs();

            Assert.Same(items[1], ((GridRow)grid.SelectedItem!).Item);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The same item twice over is two rows, not one row drawn twice.</summary>
    [AvaloniaFact]
    public void TheSameItemTwiceGetsARowEach()
    {
        var entry = new Entry("pear");
        var rows = new GridRows(new[] { entry, entry });

        Assert.Equal(2, rows.Count);
        Assert.NotSame(rows[0], rows[1]);
        Assert.Same(entry, rows[0].Item);
        Assert.Same(entry, rows[1].Item);
    }

    private static Entry[] Items(params string[] names) =>
        [.. names.Select(name => new Entry(name))];

    private static Entry[] Items(params (string Name, int Rank)[] entries) =>
        [.. entries.Select(entry => new Entry(entry.Name, entry.Rank))];

    private static GridColumn Named() => new()
    {
        Header = "NAME",
        SortKey = item => ((Entry)item).Name,
    };

    private static DataGrid Grid(GridRows rows, out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = rows;

        window = new Window { Content = grid, Width = 420, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Name, int Rank = 0);

    /// <summary>A group key that is a new object every time the key is asked for.</summary>
    private sealed class Letter(string text)
    {
        public string Text => text;
    }

    private sealed class LetterKeys : IEqualityComparer<object>
    {
        public new bool Equals(object? left, object? right) =>
            left is Letter first && right is Letter second
                ? first.Text == second.Text
                : ReferenceEquals(left, right);

        public int GetHashCode(object value) => value is Letter letter ? letter.Text.GetHashCode() : value.GetHashCode();
    }
}
