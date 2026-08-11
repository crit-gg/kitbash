using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a grid sounds like. Without peers a screen reader hears a list box of content
/// controls, and a cell reads as its text alone with nothing saying which column it is in.
/// </summary>
public sealed class GridAutomationTests
{
    /// <summary>A grid says it is a grid, not a list.</summary>
    [AvaloniaFact]
    public void TheGridIsAGrid()
    {
        var grid = Grid(out var window);

        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(grid);

            Assert.Equal(AutomationControlType.DataGrid, peer.GetAutomationControlType());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>And it carries how much is shown, the sort and what is blocking a save.</summary>
    [AvaloniaFact]
    public void TheGridSaysWhatIsOnScreen()
    {
        var grid = Grid(out var window);

        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(grid);

            Assert.Contains("3 of 3 shown", peer.GetHelpText());

            Click(grid, grid.Columns[1]);

            Assert.Contains("sorted by NAME", peer.GetHelpText());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A cell says which column it is in before it says what is in it.</summary>
    [AvaloniaFact]
    public void ACellNamesItsColumn()
    {
        var grid = Grid(out var window);

        try
        {
            var cell = Cells(grid).First(cell => cell.Column?.Header?.ToString() == "NAME");
            var peer = ControlAutomationPeer.CreatePeerForElement(cell);

            Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
            Assert.Equal("cell", peer.GetLocalizedControlType());
            Assert.Equal("NAME, b0", peer.GetName());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>What is wrong with a cell is never drawn in it, so the peer is where it is said.</summary>
    [AvaloniaFact]
    public void ACellCarriesItsError()
    {
        var grid = Grid(out var window);

        try
        {
            var cell = Cells(grid).First();

            cell.Error = "A name cannot be empty.";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(
                "A name cannot be empty.",
                ControlAutomationPeer.CreatePeerForElement(cell).GetHelpText());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A row reads as the cells in it rather than as one blob of text.</summary>
    [AvaloniaFact]
    public void ARowReadsItsCells()
    {
        var grid = Grid(out var window);

        try
        {
            var row = grid.GetVisualDescendants().OfType<DataGridRow>().First();
            var peer = ControlAutomationPeer.CreatePeerForElement(row);

            Assert.Equal(AutomationControlType.DataItem, peer.GetAutomationControlType());
            Assert.Equal("ID, a0, NAME, b0", peer.GetName());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A title says which way it is sorting, since the caret alone says nothing.</summary>
    [AvaloniaFact]
    public void ATitleSaysWhichWayItSorts()
    {
        var grid = Grid(out var window);

        try
        {
            var title = grid.GetVisualDescendants()
                .OfType<DataGridHeaderCell>()
                .First(cell => cell.Column?.Header?.ToString() == "NAME");

            var peer = ControlAutomationPeer.CreatePeerForElement(title);

            Assert.Equal(AutomationControlType.HeaderItem, peer.GetAutomationControlType());
            Assert.Equal("NAME", peer.GetName());
            Assert.Equal(string.Empty, peer.GetHelpText());

            Click(grid, grid.Columns[1]);

            Assert.Equal("sorted ascending", peer.GetHelpText());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A group heading is a heading and says how many rows it holds.</summary>
    [AvaloniaFact]
    public void AGroupHeadingSaysItsCount()
    {
        var grid = Grid(out var window);

        try
        {
            grid.Rows!.Group(item => ((Entry)item).Kind);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var heading = grid.GetVisualDescendants().OfType<GridGroupRow>().First();
            var peer = ControlAutomationPeer.CreatePeerForElement(heading);

            Assert.Equal("one, 2 rows", peer.GetName());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A left click on a column's title, which is the only way in from outside.</summary>
    private static void Click(DataGrid grid, GridColumn column)
    {
        var title = grid.GetVisualDescendants()
            .OfType<DataGridHeaderCell>()
            .First(cell => ReferenceEquals(cell.Column, column));

        title.RaiseEvent(new PointerReleasedEventArgs(
            title,
            new Pointer(0, PointerType.Mouse, true),
            title,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));

        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<DataGridCell> Cells(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>();

    private static DataGrid Grid(out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", entry => entry.Id));
        grid.Columns.Add(Column("NAME", entry => entry.Name));

        grid.ItemsSource = new GridRows(new[]
        {
            new Entry("a0", "b0", "one"),
            new Entry("a1", "b1", "one"),
            new Entry("a2", "b2", "two"),
        });

        window = new Window { Content = grid, Width = 480, Height = 220 };
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
        SortKey = item => item is Entry entry ? read(entry) : null,
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : read(entry) }),
    };

    private sealed record Entry(string Id, string Name, string Kind);
}
