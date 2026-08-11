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
/// What a person may do to the columns. Each gesture needs the grid to allow it and the
/// column to allow it, so a grid saying yes and a column saying no still means no.
/// </summary>
public sealed class GridColumnGestureTests
{
    /// <summary>A double click on the edge fits the column to what is on screen.</summary>
    [AvaloniaFact]
    public void FittingTakesTheWidestCellOnScreen()
    {
        var grid = Grid(out var window);

        try
        {
            var column = grid.Columns[0];
            var was = column.ActualWidth;

            Fit(grid, column);

            Assert.NotEqual(was, column.ActualWidth);

            // Wide enough for the longest value drawn, and nowhere near the star width it
            // had before.
            Assert.True(column.ActualWidth < was, $"fitted to {column.ActualWidth} from {was}");
            Assert.True(column.ActualWidth >= column.MinWidth);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told it may not fit does nothing when the edge is double clicked.</summary>
    [AvaloniaFact]
    public void AGridThatMayNotFitDoesNothing()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Resize;
            Dispatcher.UIThread.RunJobs();

            var column = grid.Columns[0];
            var was = column.ActualWidth;

            Fit(grid, column);

            Assert.Equal(was, column.ActualWidth);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told it may do nothing to its columns draws no grab area at all.</summary>
    [AvaloniaFact]
    public void NoGesturesLeavesNoEdgeToGrab()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.NotEmpty(Dividers(grid));

            grid.ColumnGestures = ColumnGestures.None;
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            Assert.Empty(Dividers(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Shift and a click adds a key rather than replacing the one already there.</summary>
    [AvaloniaFact]
    public void ShiftAndAClickAddsASortKey()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.MultiSort;
            Dispatcher.UIThread.RunJobs();

            Click(grid, grid.Columns[0]);
            Click(grid, grid.Columns[1], shift: true);

            Assert.Equal(2, grid.Rows!.Sorts.Count);
            Assert.Equal(1, grid.Columns[0].SortOrder);
            Assert.Equal(2, grid.Columns[1].SortOrder);
            Assert.Equal("sorted by ID, NAME", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Without the gesture a shift click replaces the key, which is what it always did.</summary>
    [AvaloniaFact]
    public void WithoutTheGestureAShiftClickReplaces()
    {
        var grid = Grid(out var window);

        try
        {
            Click(grid, grid.Columns[0]);
            Click(grid, grid.Columns[1], shift: true);

            Assert.Single(grid.Rows!.Sorts);
            Assert.Equal("sorted by NAME", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Past three keys the footer stops naming them.</summary>
    [AvaloniaFact]
    public void TheFooterStopsNamingPastThree()
    {
        var grid = Grid(out var window, columns: 5);

        try
        {
            grid.ColumnGestures = ColumnGestures.MultiSort;
            Dispatcher.UIThread.RunJobs();

            Click(grid, grid.Columns[0]);

            for (var at = 1; at < 5; at++)
            {
                Click(grid, grid.Columns[at], shift: true);
            }

            Assert.Equal("sorted by ID, NAME, COLUMN 2 and 2 more", grid.SortText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The menu offers what the grid and the column between them allow.</summary>
    [AvaloniaFact]
    public void TheMenuOffersOnlyWhatIsAllowed()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Hide;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(
                ["Sort ascending", "Sort descending", "Group by this column", "Hide column"],
                Menu(window, grid, grid.Columns[0]));

            // Sorted, so clearing it is offered too.
            Click(grid, grid.Columns[0]);

            Assert.Contains("Clear sort on this column", Menu(window, grid, grid.Columns[0]));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid that may not hide a column does not offer to.</summary>
    [AvaloniaFact]
    public void HidingNeedsTheGesture()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.DoesNotContain("Hide column", Menu(window, grid, grid.Columns[0]));

            grid.ColumnGestures = ColumnGestures.Hide;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("Hide column", Menu(window, grid, grid.Columns[0]));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Hiding takes the column off and the rest take the room back.</summary>
    [AvaloniaFact]
    public void HidingTakesTheColumnOff()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Hide;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, grid.Columns.Reachable.Count);

            grid.Columns[0].IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            Assert.Equal(2, grid.Columns.Reachable.Count);

            // And the last one showing can never be taken off.
            grid.Columns[1].IsVisible = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(grid.Columns.CanHide(grid.Columns[2]));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The ordinal beside the caret only shows in a sort of more than one.</summary>
    [AvaloniaFact]
    public void TheOrdinalOnlyShowsWithASecondKey()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.MultiSort;
            Dispatcher.UIThread.RunJobs();

            Click(grid, grid.Columns[0]);

            Assert.False(Title(grid, grid.Columns[0]).HasOrdinal);

            Click(grid, grid.Columns[1], shift: true);

            Assert.True(Title(grid, grid.Columns[0]).HasOrdinal);
            Assert.Equal("1", Title(grid, grid.Columns[0]).Ordinal);
            Assert.Equal("2", Title(grid, grid.Columns[1]).Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Dragging a title moves the column to where the line was.</summary>
    [AvaloniaFact]
    public void DraggingATitleMovesTheColumn()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Reorder;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["ID", "NAME", "COLUMN 2"], Order(grid));

            // Dropped in the gap before the last column, so it lands second rather than last.
            Move(grid, grid.Columns[0], grid.Columns[2].Offset);

            Assert.Equal(["NAME", "ID", "COLUMN 2"], Order(grid));

            // And dropped past the far edge it goes to the end.
            Move(grid, grid.Columns[1], grid.Columns.TotalWidth);

            Assert.Equal(["NAME", "COLUMN 2", "ID"], Order(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Without the gesture a title drag does nothing at all.</summary>
    [AvaloniaFact]
    public void ReorderNeedsTheGesture()
    {
        var grid = Grid(out var window);

        try
        {
            Move(grid, grid.Columns[0], grid.Columns[2].Offset);

            Assert.Equal(["ID", "NAME", "COLUMN 2"], Order(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A drag too small to be one is still a click, so sorting survives.</summary>
    [AvaloniaFact]
    public void AShakeIsStillAClick()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Reorder;
            Dispatcher.UIThread.RunJobs();

            var title = Title(grid, grid.Columns[0]);

            title.RaiseEvent(Down(title, new Point(2, 8)));
            title.RaiseEvent(Over(title, new Point(4, 8)));
            title.RaiseEvent(Up(title));

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["ID", "NAME", "COLUMN 2"], Order(grid));
            Assert.Equal(GridSortDirection.Ascending, grid.Columns[0].SortDirection);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A pinned column takes the left edge and the seam falls where it ends.</summary>
    [AvaloniaFact]
    public void PinningHoldsAColumnAgainstTheLeftEdge()
    {
        var grid = Grid(out var window);

        try
        {
            grid.ColumnGestures = ColumnGestures.Pin;
            Dispatcher.UIThread.RunJobs();

            // Nothing pinned is the ordinary layout, and the seam is not there at all.
            Assert.Equal(0, grid.PinnedWidth);
            Assert.Equal(0, grid.Columns[0].Offset);

            // The last column pinned goes to the front, whatever order it was declared in.
            grid.Columns[2].IsPinned = true;
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, grid.Columns[2].Offset);
            Assert.Equal(grid.Columns[2].ActualWidth, grid.PinnedWidth);
            Assert.Equal(grid.PinnedWidth, grid.Columns[0].Offset);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Pinning needs the gesture, and the last unpinned column can never be pinned.</summary>
    [AvaloniaFact]
    public void PinningNeedsTheGestureAndSomethingLeftToScroll()
    {
        var grid = Grid(out var window);

        try
        {
            Assert.False(grid.Columns.CanPin(grid.Columns[0]));

            grid.ColumnGestures = ColumnGestures.Pin;
            Dispatcher.UIThread.RunJobs();

            Assert.True(grid.Columns.CanPin(grid.Columns[0]));
            Assert.Contains("Pin to the left", Menu(window, grid, grid.Columns[0]));

            grid.Columns[0].IsPinned = true;
            grid.Columns[1].IsPinned = true;
            Dispatcher.UIThread.RunJobs();

            // One column left over, so pinning that one too would leave nothing to scroll.
            Assert.False(grid.Columns.CanPin(grid.Columns[2]));

            // And the one already pinned still offers to be let go.
            Assert.True(grid.Columns.CanPin(grid.Columns[0]));
        }
        finally
        {
            window.Close();
        }
    }

    private static List<string> Order(DataGrid grid) =>
        [.. grid.Columns.Select(column => column.Header?.ToString() ?? string.Empty)];

    /// <summary>Picks a title up and drops it at an offset measured across the header.</summary>
    private static void Move(DataGrid grid, GridColumn column, double to)
    {
        var title = Title(grid, column);

        title.RaiseEvent(Down(title, new Point(4, 8)));
        title.RaiseEvent(Over(title, new Point(to, 8)));
        title.RaiseEvent(Up(title));

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static PointerPressedEventArgs Down(Control target, Point at) => new(
        target,
        new Pointer(0, PointerType.Mouse, true),
        target,
        at,
        0,
        new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
        KeyModifiers.None);

    private static PointerEventArgs Over(Control target, Point at) => new(
        InputElement.PointerMovedEvent,
        target,
        new Pointer(0, PointerType.Mouse, true),
        target,
        at,
        0,
        new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
        KeyModifiers.None);

    private static PointerReleasedEventArgs Up(Control target) => new(
        target,
        new Pointer(0, PointerType.Mouse, true),
        target,
        default,
        0,
        new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
        KeyModifiers.None,
        MouseButton.Left);

    /// <summary>
    /// What the menu offers for a column, in the order it offers it. The chevron is pressed
    /// and the items are read off the flyout that opens, so the menu itself is what is under
    /// test rather than a list built beside it.
    /// </summary>
    private static List<string> Menu(Window window, DataGrid grid, GridColumn column)
    {
        var title = Title(grid, column);
        var chevron = Chevron(title);

        chevron.RaiseEvent(new PointerPressedEventArgs(
            chevron,
            new Pointer(0, PointerType.Mouse, true),
            chevron,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return
        [
            .. window.GetVisualDescendants()
                .OfType<MenuItem>()
                .Select(item => item.Header?.ToString() ?? string.Empty),
        ];
    }

    private static Border Chevron(DataGridHeaderCell title) =>
        title.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Menu");

    private static DataGridHeaderCell Title(DataGrid grid, GridColumn column) =>
        grid.GetVisualDescendants()
            .OfType<DataGridHeaderCell>()
            .First(cell => ReferenceEquals(cell.Column, column));

    /// <summary>A left click on a column's title, which is the only way in from outside.</summary>
    private static void Click(DataGrid grid, GridColumn column, bool shift = false)
    {
        var title = Title(grid, column);

        title.RaiseEvent(new PointerReleasedEventArgs(
            title,
            new Pointer(0, PointerType.Mouse, true),
            title,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            shift ? KeyModifiers.Shift : KeyModifiers.None,
            MouseButton.Left));

        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<ColumnDivider> Dividers(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<ColumnDivider>().Where(divider => divider.IsVisible);

    /// <summary>A double click on the edge that belongs to a column.</summary>
    private static void Fit(DataGrid grid, GridColumn column)
    {
        var divider = grid.GetVisualDescendants()
            .OfType<ColumnDivider>()
            .First(divider => ReferenceEquals(GridCells.GetColumn(divider), column));

        divider.RaiseEvent(new PointerPressedEventArgs(
            divider,
            new Pointer(0, PointerType.Mouse, true),
            divider,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None,
            2));

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGrid Grid(out Window window, int columns = 3)
    {
        var grid = new DataGrid();

        string[] names = ["ID", "NAME", "COLUMN 2", "COLUMN 3", "COLUMN 4"];

        for (var at = 0; at < columns; at++)
        {
            var name = names[at];

            grid.Columns.Add(new GridColumn
            {
                Header = name,
                Width = new GridLength(1, GridUnitType.Star),
                CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
                SortKey = item => ((Entry)item).Name,
            });
        }

        grid.ItemsSource = new GridRows(
            Enumerable.Range(0, 3).Select(number => new Entry($"r{number}")).ToList());

        window = new Window { Content = grid, Width = 720, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Name);
}
