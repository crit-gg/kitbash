using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a column holds, and the affordance a cell offers because of it. A cell draws its
/// value through the cell template until a double click opens an editor, so the chevron, the
/// jump mark and the stepper cannot come from the editor and are the cell's own.
/// </summary>
public sealed class GridCellKindTests
{
    [AvaloniaTheory]
    [InlineData(GridCellKind.Text, ":text")]
    [InlineData(GridCellKind.Enum, ":enum")]
    [InlineData(GridCellKind.Number, ":number")]
    [InlineData(GridCellKind.Reference, ":reference")]
    [InlineData(GridCellKind.Tags, ":tags")]
    [InlineData(GridCellKind.Ratio, ":ratio")]
    [InlineData(GridCellKind.Colour, ":colour")]
    [InlineData(GridCellKind.Boolean, ":boolean")]
    public void AKindIsAClassOnTheCell(GridCellKind kind, string name)
    {
        var grid = Grid(out var window, kind);

        try
        {
            var cell = Cells(grid).First();

            Assert.Contains(name, cell.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Plain is the default and carries no class at all.</summary>
    [AvaloniaFact]
    public void PlainSaysNothing()
    {
        var grid = Grid(out var window, GridCellKind.Plain);

        try
        {
            var cell = Cells(grid).First();

            Assert.DoesNotContain(cell.Classes, name => name.StartsWith(':') && name is not ":editable");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Changing a column's kind moves the class with it.</summary>
    [AvaloniaFact]
    public void ChangingTheKindMovesTheClass()
    {
        var grid = Grid(out var window, GridCellKind.Text);

        try
        {
            grid.Columns[0].Kind = GridCellKind.Enum;
            Dispatcher.UIThread.RunJobs();

            var cell = Cells(grid).First();

            Assert.Contains(":enum", cell.Classes);
            Assert.DoesNotContain(":text", cell.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The two kinds that carry a trailing affordance reserve its room from the start, so a
    /// value never reflows the moment a pointer arrives. A number has none, since a stepper
    /// in a cell is a widget in a column that is supposed to read as numbers. A reference
    /// reserves nothing until it is told where it goes, which is the test below.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(GridCellKind.Enum, true)]
    [InlineData(GridCellKind.Reference, false)]
    [InlineData(GridCellKind.Number, false)]
    [InlineData(GridCellKind.Text, false)]
    [InlineData(GridCellKind.Boolean, false)]
    [InlineData(GridCellKind.Plain, false)]
    public void OnlyTwoKindsReserveTheTrailingEdge(GridCellKind kind, bool reserves)
    {
        var grid = Grid(out var window, kind);

        try
        {
            Assert.Equal(reserves, Part(grid, "PART_Affordance")?.IsVisible == true);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A reference draws its jump only when the column says where it goes. A mark that
    /// answers nothing is worse than no mark at all.
    /// </summary>
    [AvaloniaFact]
    public void AReferenceReservesTheEdgeOnlyWhenItGoesSomewhere()
    {
        var grid = Grid(out var window, GridCellKind.Reference, jumps: true);

        try
        {
            Assert.True(Part(grid, "PART_Affordance")?.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The room is held whether or not the glyph is drawn, so a value does not shift
    /// sideways the moment a pointer arrives. The glyph alone reserved nothing, since an
    /// element that is not visible is not laid out either.
    /// </summary>
    [AvaloniaFact]
    public void TheRoomIsHeldWhileTheGlyphIsNot()
    {
        var grid = Grid(out var window, GridCellKind.Enum);

        try
        {
            var mark = Part(grid, "PART_Affordance");
            var chevron = Part(grid, "PART_Chevron");

            Assert.False(chevron?.IsVisible);
            Assert.True(mark?.Bounds.Width > 0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A read only column reserves nothing, since none of the three applies to it.</summary>
    [AvaloniaFact]
    public void AReadOnlyColumnReservesNothing()
    {
        var grid = Grid(out var window, GridCellKind.Enum, editable: false);

        try
        {
            Assert.False(Part(grid, "PART_Affordance")?.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A read only cell holds the resting ink and never lifts off it, which is the whole
    /// signal. It is the cell that does not react, not the cell that is dimmer.
    /// </summary>
    [AvaloniaFact]
    public void AReadOnlyCellRestsQuieter()
    {
        var grid = Grid(out var window, GridCellKind.Text, editable: false);

        try
        {
            var cell = Cells(grid).First();

            Assert.Equal(window.FindResource("InkSecondary"), cell.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A control inside a cell draws no frame of its own, since the cell is already drawing
    /// the hint, the ring and the well.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("GridCellText")]
    [InlineData("GridCellNumber")]
    [InlineData("GridCellChoice")]
    [InlineData("GridCellColor")]
    public void NothingInsideACellDrawsAFrame(string key)
    {
        var window = new Window();

        try
        {
            var theme = (ControlTheme)window.FindResource(key)!;

            Assert.Equal(new Thickness(0), Of<Thickness>(theme, "BorderThickness"));
            Assert.Equal(new CornerRadius(0), Of<CornerRadius>(theme, "CornerRadius"));
            Assert.Equal(new Thickness(0), Of<Thickness>(theme, "Padding"));
        }
        finally
        {
            window.Close();
        }
    }

    private static T Of<T>(ControlTheme theme, string property)
    {
        var setter = theme.Setters
            .OfType<Setter>()
            .FirstOrDefault(setter => setter.Property?.Name == property);

        Assert.NotNull(setter);

        return (T)setter!.Value!;
    }

    private static Control? Part(DataGrid grid, string name) =>
        grid.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == name);

    private static IEnumerable<DataGridCell> Cells(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>();

    private static DataGrid Grid(
        out Window window, GridCellKind kind, bool editable = true, bool jumps = false)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "VALUE",
            Kind = kind,
            Jump = jumps ? _ => { } : null,
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            EditTemplate = editable
                ? new FuncDataTemplate<Entry>((_, _) => new TextBox())
                : null,
        });

        grid.ItemsSource = new GridRows(new[] { new Entry("one") });

        window = new Window { Content = grid, Width = 420, Height = 200 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed record Entry(string Name);
}
