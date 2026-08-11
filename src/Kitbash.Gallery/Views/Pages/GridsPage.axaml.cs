using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Both grids, over one column model.</summary>
public partial class GridsPage : GalleryPage
{
    public GridsPage()
    {
        InitializeComponent();

        BuildGrids();
    }

    /// <summary>
    /// Ten thousand rows for the flat grid and a small hierarchy for the tree grid. The
    /// sort keys are set here because a column takes a function and XAML cannot write one.
    /// </summary>
    private void BuildGrids()
    {
        string[] kinds = ["Table", "Graph", "Sheet", "List"];
        (PillStatus Status, string Text)[] states =
        [
            (PillStatus.Ok, "synced"),
            (PillStatus.Modified, "modified"),
            (PillStatus.Accent, "checked out"),
            (PillStatus.Error, "conflict"),
            (PillStatus.Neutral, "archived"),
        ];

        var entries = new List<Entry>();

        for (var index = 1; index <= 10_000; index++)
        {
            var state = states[index % states.Length];

            entries.Add(new Entry(
                $"ROW_{index:0000}_{kinds[index % kinds.Length]}",
                $"Row number {index}",
                kinds[index % kinds.Length],
                Math.Round(1 + (index % 97) * 0.5, 1),
                index % 5,
                state.Status,
                state.Text));
        }

        Key(SampleGrid.Columns[1], entry => entry.Id);
        Key(SampleGrid.Columns[2], entry => entry.Name);
        Key(SampleGrid.Columns[3], entry => entry.Kind);
        Key(SampleGrid.Columns[4], entry => entry.Value);
        Key(SampleGrid.Columns[5], entry => entry.Tier);
        Key(SampleGrid.Columns[6], entry => entry.State);

        SampleGrid.ItemsSource = new GridRows(entries);
        SampleGrid.SelectionChanged += (_, _) => ShowPickAll();
        SamplePager.Rows = SampleGrid.Rows;

        // The name column, since a picker stands in front of it and only a caller knows that.
        SampleTreeGrid.LeadColumn = SampleTreeGrid.Columns[1];

        List<Aggregate> roots =
        [
            new("First branch", "4 kinds", "avg 9.4", "1.8x", "3 modified",
            [
                new("A leaf", "Table", "6.0", "1.0x", "synced"),
                new("Another leaf", "Table", "8.0", "2.4x", "modified"),
                new("A branch inside it", "Graph", "14.0", "3.1x", "checked out",
                [
                    new("Deeper", "Graph", "16.5", "2.8x", "synced"),
                    new("Deeper again", "Graph", "11.0", "2.2x", "synced"),
                ]),
            ]),
            new("Second branch", "9 kinds", "avg 4.2", "1.2x", "all synced",
            [
                new("A leaf keeps the caret's room", "Sheet", "4.0", "1.1x", "synced"),
                new("Another", "Sheet", "4.4", "1.3x", "synced"),
            ]),
            new("A branch with nothing in it", "0 kinds", "-", "-", "empty"),
        ];

        var opened = new TreeRows(roots, item => ((Aggregate)item).Children);

        // Opened, so the indent and a branch inside a branch are on the page rather than a
        // click away.
        opened.Expand(opened[0]);
        opened.Expand(opened[3]);

        SampleTreeGrid.ItemsSource = opened;
    }

    private static void Key(GridColumn column, Func<Entry, object?> key) =>
        column.SortKey = item => key((Entry)item);

    /// <summary>Picks every row on the page, or none of them.</summary>
    private void OnPickAll(object? sender, RoutedEventArgs e)
    {
        if (PickAll.IsChecked == true)
        {
            SampleGrid.SelectAll();
        }
        else
        {
            SampleGrid.UnselectAll();
        }
    }

    private void ShowPickAll()
    {
        var picked = SampleGrid.SelectedItems?.Count ?? 0;

        PickAll.IsChecked = picked == 0 ? false : picked >= SampleGrid.ItemCount ? true : null;
    }

    private void OnPlainGrid(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: var on })
        {
            SampleGrid.Classes.Set("plain", on is true);
        }
    }

    private void OnGroupGrid(object? sender, RoutedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows || sender is not Button button)
        {
            return;
        }

        var grouped = rows.GroupKey is not null;

        rows.Group(grouped ? null : item => ((Entry)item).Kind);
        button.Content = grouped ? "Group by kind" : "Drop the grouping";
    }

    private void OnPageGrid(object? sender, RoutedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows || sender is not Button button)
        {
            return;
        }

        var paging = rows.PageSize > 0;

        rows.PageSize = paging ? 0 : SamplePager.PageSize;
        SamplePager.IsVisible = !paging;
        button.Content = paging ? "Turn paging on" : "Turn paging off";
    }

    /// <summary>
    /// The filter sits on the rows rather than on the source, so the footer still says how
    /// many rows there are as well as how many are drawn.
    /// </summary>
    private void OnFilterGrid(object? sender, TextChangedEventArgs e)
    {
        if (SampleGrid.Rows is not { } rows)
        {
            return;
        }

        var text = GridFilter.Text;

        rows.Filter = string.IsNullOrWhiteSpace(text)
            ? null
            : item => ((Entry)item).Id.Contains(text, StringComparison.OrdinalIgnoreCase)
                || ((Entry)item).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hides a column, which is the whole of what a column chooser would do.</summary>
    private void OnHideColumn(object? sender, RoutedEventArgs e)
    {
        var column = SampleGrid.Columns[3];

        column.IsVisible = !column.IsVisible;
    }

    private void OnCountCells(object? sender, RoutedEventArgs e)
    {
        var rows = SampleGrid.Rows?.Count ?? 0;
        var real = SampleGrid.GetVisualDescendants().OfType<DataGridRow>().Count();
        var cells = SampleGrid.GetVisualDescendants().OfType<DataGridCell>().Count();

        GridCount.Text = $"{rows} rows, {real} of them are controls, holding {cells} cells";
    }
}
