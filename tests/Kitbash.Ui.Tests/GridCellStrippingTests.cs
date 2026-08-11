using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The cell is the shape, so nothing inside one keeps a frame, a radius, padding or a focus
/// halo of its own. Measured on the realised parts rather than on the themes, since a theme
/// says what it sets and a template child can still bring its own.
/// </summary>
public sealed class GridCellStrippingTests
{
    [AvaloniaTheory]
    [InlineData("GridCellText")]
    [InlineData("GridCellNumber")]
    [InlineData("GridCellChoice")]
    public void AnEditorInACellDrawsNoFrame(string key)
    {
        var grid = Editing(key, out var window);

        try
        {
            var frames = Parts(grid, "PART_Frame").ToList();

            Assert.NotEmpty(frames);

            foreach (var frame in frames)
            {
                Assert.Equal(new Thickness(0), frame.BorderThickness);
                Assert.Equal(new CornerRadius(0), frame.CornerRadius);
                Assert.Equal(new Thickness(0), frame.Padding);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("GridCellText")]
    [InlineData("GridCellNumber")]
    [InlineData("GridCellChoice")]
    public void AnEditorInACellDrawsNoHalo(string key)
    {
        var grid = Editing(key, out var window);

        try
        {
            // Taken out rather than made transparent. Its thickness and its reach are
            // literals in each template, so a colourless halo would still hold its reach.
            var halos = Parts(grid, "PART_Halo").Concat(Parts(grid, "PART_Focus")).ToList();

            Assert.NotEmpty(halos);
            Assert.DoesNotContain(halos, halo => halo.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The chevron is the cell's, so a combo box in one draws none of its own.</summary>
    [AvaloniaFact]
    public void AComboBoxInACellDrawsNoChevronOfItsOwn()
    {
        var grid = Editing("GridCellChoice", out var window);

        try
        {
            var chevrons = grid.GetVisualDescendants()
                .OfType<Icon>()
                .Where(icon => icon.Name == "PART_Chevron")
                .ToList();

            Assert.NotEmpty(chevrons);
            Assert.DoesNotContain(chevrons, icon => icon.IsVisible && !IsCellsOwn(icon));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A number cell offers no stepper at any point, hovered or not.</summary>
    [AvaloniaFact]
    public void ANumberCellOffersNoStepper()
    {
        var grid = Editing("GridCellNumber", out var window);

        try
        {
            var spinners = grid.GetVisualDescendants()
                .OfType<ButtonSpinner>()
                .Where(spinner => spinner.ShowButtonSpinner);

            Assert.Empty(spinners);
            Assert.DoesNotContain(
                grid.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "PART_Stepper");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The cell's own chevron is the one that stays, so it is told from the field's.</summary>
    private static bool IsCellsOwn(Visual icon) =>
        icon.FindAncestorOfType<ComboBox>() is null;

    /// <summary>
    /// Named parts inside the cell alone. The row has a PART_Focus of its own, which is the
    /// grid's deliberate answer to focus and has to stay.
    /// </summary>
    private static IEnumerable<Border> Parts(DataGrid grid, string name) =>
        grid.GetVisualDescendants()
            .OfType<DataGridCell>()
            .SelectMany(cell => cell.GetVisualDescendants().OfType<Border>())
            .Where(border => border.Name == name);

    /// <summary>A grid with one cell open for editing, drawing the form under test.</summary>
    private static DataGrid Editing(string key, out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "VALUE",
            Kind = GridCellKind.Text,
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            EditTemplate = new FuncDataTemplate<Entry>((_, _) => Form(key)),
        });

        grid.ItemsSource = new GridRows(new[] { new Entry("one") });

        window = new Window { Content = grid, Width = 420, Height = 200 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        grid.BeginEdit(grid.GetVisualDescendants().OfType<DataGridCell>().First());

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static Control Form(string key)
    {
        var theme = Application.Current!.FindResource(key) as ControlTheme;

        return key switch
        {
            "GridCellNumber" => new NumericUpDown { Theme = theme },
            "GridCellChoice" => new ComboBox { Theme = theme, ItemsSource = new[] { "one", "two" } },
            _ => new TextBox { Theme = theme },
        };
    }

    private sealed record Entry(string Name);
}
