using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a real pointer does to a grid. Every other pointer test raises its events straight
/// at the control it means, which skips hit testing and skips the capture Avalonia takes on
/// a press, and both of those are where the gestures here went wrong.
/// </summary>
public sealed class GridPointerTests
{
    /// <summary>Where the reference column's jump went, in the order it was pressed.</summary>
    private readonly List<string> went = [];

    /// <summary>
    /// Dragging across the cells takes the block with it. The pointer is captured by the
    /// cell that was pressed, so the ones it travels over never hear a move and the block
    /// stayed one cell wide until the body started working out what is under the pointer.
    /// </summary>
    [AvaloniaFact]
    public void ADragAcrossTheCellsTakesTheBlock()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            Press(window, grid, 0, "ID");
            Drag(window, grid, 2, "KIND");

            // Three rows by three columns, less the anchor, which wears no wash.
            Assert.Equal(8, InRange(grid).Count);

            Release(window, grid, 2, "KIND");

            Assert.Equal(8, InRange(grid).Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The handle at the corner of the block drags the block's values over the rest.</summary>
    [AvaloniaFact]
    public void TheHandleFillsWhatItIsDraggedOver()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell, out var items);

        try
        {
            Press(window, grid, 0, "ID");
            Release(window, grid, 0, "ID");

            var handle = Cell(grid, 0, "ID");

            Assert.True(handle.HasHandle);

            window.MouseDown(Corner(window, handle), MouseButton.Left);
            Pump(window);

            Drag(window, grid, 2, "ID");
            Release(window, grid, 2, "ID");

            Assert.Equal("a0", items[1].Id);
            Assert.Equal("a0", items[2].Id);

            // Nothing outside the reach of the fill, and nothing in another column.
            Assert.Equal("a3", items[3].Id);
            Assert.Equal("b1", items[1].Name);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Shift and a click extends the run of rows, which is the list's own gesture. The cell
    /// used to take the press whatever the unit was, so it never reached the list.
    /// </summary>
    [AvaloniaFact]
    public void ShiftAndAClickExtendsTheRows()
    {
        var grid = Grid(out var window);

        try
        {
            Press(window, grid, 0, "ID");
            Release(window, grid, 0, "ID");

            Assert.Equal(1, grid.SelectedItems?.Count);

            Press(window, grid, 2, "ID", RawInputModifiers.Shift);
            Release(window, grid, 2, "ID", RawInputModifiers.Shift);

            Assert.Equal(3, grid.SelectedItems?.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>In cell units the same gesture takes the block out instead.</summary>
    [AvaloniaFact]
    public void ShiftAndAClickTakesTheBlockInCellUnits()
    {
        var grid = Grid(out var window, GridSelectionUnit.Cell);

        try
        {
            Press(window, grid, 0, "ID");
            Release(window, grid, 0, "ID");

            Press(window, grid, 2, "NAME", RawInputModifiers.Shift);
            Release(window, grid, 2, "NAME", RawInputModifiers.Shift);

            Assert.Equal(5, InRange(grid).Count);
            Assert.Equal(1, grid.SelectedItems?.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A choice arrives open. Opening the editor is what a person asked for and a list is
    /// what a choice is, so the click that opens the cell must not need a second one after
    /// it to see the values.
    /// </summary>
    [AvaloniaFact]
    public void AChoiceEditorArrivesOpen()
    {
        var grid = Grid(out var window);

        try
        {
            Press(window, grid, 0, "KIND");
            Release(window, grid, 0, "KIND");
            Press(window, grid, 0, "KIND");
            Release(window, grid, 0, "KIND");

            Assert.True(Cell(grid, 0, "KIND").IsEditing);
            Assert.Contains(grid.GetVisualDescendants().OfType<ComboBox>(), box => box.IsDropDownOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// One click on the chevron opens the list. The mark is a promise, and a promise that
    /// takes a second gesture to answer is what makes it read as decoration.
    /// </summary>
    [AvaloniaFact]
    public void AClickOnTheChevronOpensTheList()
    {
        var grid = Grid(out var window);

        try
        {
            var cell = Cell(grid, 0, "KIND");

            window.MouseDown(Trailing(window, cell), MouseButton.Left);
            window.MouseUp(Trailing(window, cell), MouseButton.Left);
            Pump(window);

            Assert.True(cell.IsEditing);
            Assert.Contains(grid.GetVisualDescendants().OfType<ComboBox>(), box => box.IsDropDownOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A click on the jump goes where the column says, and opens no editor.</summary>
    [AvaloniaFact]
    public void AClickOnTheJumpGoesWhereItSays()
    {
        var grid = Grid(out var window, GridSelectionUnit.Row, out var items);

        try
        {
            var cell = Cell(grid, 0, "REF");

            window.MouseDown(Trailing(window, cell), MouseButton.Left);
            window.MouseUp(Trailing(window, cell), MouseButton.Left);
            Pump(window);

            Assert.Equal([items[0].Id], went);
            Assert.False(cell.IsEditing);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A grid that opens no editors draws no chevron, since the whole of what it promises
    /// is an edit that cannot happen.
    /// </summary>
    [AvaloniaFact]
    public void AReadOnlyGridDrawsNoChevron()
    {
        var grid = Grid(out var window);

        try
        {
            var cell = Cell(grid, 0, "KIND");

            Assert.Contains(":editable", cell.Classes);

            grid.BeginEditGestures = BeginEditGestures.None;
            Pump(window);

            Assert.DoesNotContain(":editable", cell.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The middle of the mark at a cell's trailing edge.</summary>
    private static Point Trailing(Window window, DataGridCell cell)
    {
        var mark = cell.GetVisualDescendants()
            .OfType<Panel>()
            .Single(panel => panel.Name == "PART_Affordance");

        return mark.TranslatePoint(new Point(mark.Bounds.Width / 2, mark.Bounds.Height / 2), window)!.Value;
    }

    private static List<DataGridCell> InRange(DataGrid grid) =>
        [.. grid.GetVisualDescendants().OfType<DataGridCell>().Where(cell => cell.IsInRange)];

    private static DataGridCell Cell(DataGrid grid, int row, string header) =>
        (grid.ContainerFromIndex(row) as Visual)!
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .Single(cell => (string?)cell.Column?.Header == header);

    private static void Press(
        Window window, DataGrid grid, int row, string header, RawInputModifiers held = RawInputModifiers.None)
    {
        window.MouseDown(At(window, grid, row, header), MouseButton.Left, held);
        Pump(window);
    }

    /// <summary>
    /// The pointer moving with the button down. The modifier has to say the button is still
    /// held, since a headless move carries no state of its own.
    /// </summary>
    private static void Drag(Window window, DataGrid grid, int row, string header)
    {
        window.MouseMove(At(window, grid, row, header), RawInputModifiers.LeftMouseButton);
        Pump(window);
    }

    private static void Release(
        Window window, DataGrid grid, int row, string header, RawInputModifiers held = RawInputModifiers.None)
    {
        window.MouseUp(At(window, grid, row, header), MouseButton.Left, held);
        Pump(window);
    }

    /// <summary>The middle of one cell, in the window's own coordinates.</summary>
    private static Point At(Window window, DataGrid grid, int row, string header)
    {
        var cell = Cell(grid, row, header);

        return cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), window)!.Value;
    }

    /// <summary>The bottom right corner of a cell, which is where the fill handle sits.</summary>
    private static Point Corner(Window window, DataGridCell cell) =>
        cell.TranslatePoint(new Point(cell.Bounds.Width - 1, cell.Bounds.Height - 1), window)!.Value;

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private DataGrid Grid(out Window window, GridSelectionUnit unit = GridSelectionUnit.Row) =>
        Grid(out window, unit, out _);

    private DataGrid Grid(out Window window, GridSelectionUnit unit, out Entry[] items)
    {
        items = [.. Enumerable.Range(0, 4).Select(number => new Entry($"a{number}", $"b{number}", "one"))];

        var grid = new DataGrid
        {
            SelectionUnit = unit,
            SelectionMode = SelectionMode.Multiple,
            CellActions = CellActions.Copy | CellActions.Fill,
        };

        grid.Columns.Add(Column("ID", entry => entry.Id, (entry, text) => entry.Id = text ?? string.Empty));
        grid.Columns.Add(Column("NAME", entry => entry.Name, (entry, text) => entry.Name = text ?? string.Empty));
        grid.Columns.Add(Choice("KIND", entry => entry.Kind));
        grid.Columns.Add(Reference("REF", entry => entry.Id));

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 520, Height = 320 };
        window.Show();

        Pump(window);

        return grid;
    }

    private static GridColumn Column(string header, Func<Entry, object?> value, Action<Entry, string?> write) => new()
    {
        Header = header,
        Width = new GridLength(1, GridUnitType.Star),
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : value(entry)?.ToString() }),
        Value = item => value((Entry)item),
        Write = (item, text) => write((Entry)item, text),
    };

    /// <summary>A column that points at another record, with somewhere for its mark to go.</summary>
    private GridColumn Reference(string header, Func<Entry, object?> value)
    {
        var column = Column(header, value, (entry, text) => entry.Id = text ?? string.Empty);

        column.Kind = GridCellKind.Reference;
        column.Jump = item => went.Add(((Entry)item).Id);

        return column;
    }

    private static GridColumn Choice(string header, Func<Entry, object?> value)
    {
        var column = Column(header, value, (entry, text) => entry.Kind = text ?? string.Empty);

        column.Kind = GridCellKind.Enum;
        column.EditTemplate = new FuncDataTemplate<Entry>((_, _) => new ComboBox
        {
            ItemsSource = new[] { "one", "two", "three" },
        });

        return column;
    }

    private sealed class Entry(string id, string name, string kind)
    {
        public string Id { get; set; } = id;

        public string Name { get; set; } = name;

        public string Kind { get; set; } = kind;
    }
}
