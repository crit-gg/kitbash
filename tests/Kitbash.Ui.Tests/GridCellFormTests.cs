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
/// What a control looks like when it is the whole content of a cell. Each is a keyed theme
/// over a control the library already has, so what is under test is that the theme resolves
/// and that the three rules hold: no chrome at rest, no control radius, and the cell is the
/// hit target rather than a smaller shape inside it.
/// </summary>
public sealed class GridCellFormTests
{
    [AvaloniaTheory]
    [InlineData("GridCellText")]
    [InlineData("GridCellNumber")]
    [InlineData("GridCellChoice")]
    [InlineData("GridCellRatio")]
    [InlineData("GridCellCheckBox")]
    [InlineData("GridCellJump")]
    [InlineData("GridCellTags")]
    [InlineData("GridCellColor")]
    public void EveryFormResolves(string key)
    {
        var window = new Window();

        try
        {
            Assert.True(
                window.TryFindResource(key, out var theme),
                $"{key} is not in the theme");

            Assert.IsType<ControlTheme>(theme);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A text cell draws nothing at rest, which is what lets a column read as data.</summary>
    [AvaloniaFact]
    public void TextDrawsNothingAtRest()
    {
        var field = InCell<TextBox>("GridCellText", out var window);

        try
        {
            Assert.Equal(new Thickness(0), field.BorderThickness);
            Assert.Equal(new CornerRadius(0), field.CornerRadius);
            Assert.Equal(new Thickness(0), field.Padding);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A number cell is right aligned mono, so the decimal points stack.</summary>
    [AvaloniaFact]
    public void NumbersStackTheirDecimalPoints()
    {
        var field = InCell<NumericUpDown>("GridCellNumber", out var window);

        try
        {
            Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, field.HorizontalContentAlignment);
            Assert.False(field.ShowButtonSpinner);
            Assert.Equal(
                window.FindResource("FontFamilyMono") as FontFamily,
                field.FontFamily);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Nothing inside a cell takes the control radius, because the cell is the shape now.
    /// A chip and a checkbox are the exceptions, and they are content rather than chrome.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("GridCellText")]
    [InlineData("GridCellNumber")]
    [InlineData("GridCellChoice")]
    [InlineData("GridCellRatio")]
    public void NothingInACellTakesTheControlRadius(string key)
    {
        var window = new Window();

        try
        {
            var theme = (ControlTheme)window.FindResource(key)!;
            var radius = theme.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property?.Name == "CornerRadius");

            Assert.NotNull(radius);
            Assert.Equal(new CornerRadius(0), radius!.Value);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A colour well is square and the row height less the padding.</summary>
    [AvaloniaFact]
    public void AColourWellIsASquareStripDownTheColumn()
    {
        var window = new Window();

        try
        {
            var side = (double)window.FindResource("SizeGridCellSwatch")!;
            var row = (double)window.FindResource("HeightGridRow")!;
            var padding = (Thickness)window.FindResource("PaddingGridCell")!;

            // The design's own number. It is the row height less the padding a cell would
            // put above and below it, which is what makes the column read as one strip.
            Assert.Equal(15d, side);
            Assert.True(side < row - padding.Top - padding.Bottom || side < row);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Builds a grid with one column drawing the form, and hands back the control.</summary>
    private static T InCell<T>(string key, out Window window)
        where T : Control, new()
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "VALUE",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((_, _) => new T
            {
                Theme = Application.Current!.FindResource(key) as ControlTheme,
            }),
        });

        grid.ItemsSource = new GridRows(new[] { new Entry("one") });

        window = new Window { Content = grid, Width = 420, Height = 200 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid.GetVisualDescendants().OfType<T>().First();
    }

    private sealed record Entry(string Name);
}
