using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A segmented row at both densities. The row's height is a density token, so an option
/// carrying a number of its own is right at one density and floats inside the row at the
/// other, and the thumb reads the option rather than the row.
/// </summary>
public sealed class SegmentedTests
{
    /// <summary>The row, laid out for real, with the second option chosen.</summary>
    private static (Window Window, Segmented Row) Shown(bool comfortable, bool compact = false)
    {
        var row = new Segmented();

        row.Items.Add(new RadioButton { Content = "Mine" });
        row.Items.Add(new RadioButton { Content = "Theirs", IsChecked = true });

        if (compact)
        {
            row.Classes.Add("compact");
        }

        var window = new Window { Width = 400, Height = 200, Content = row };

        if (comfortable)
        {
            // The second density is one style include after the theme, which is exactly how
            // an app that browses rather than edits takes it.
            window.Styles.Add(new StyleInclude(new Uri("avares://Kitbash.Ui/"))
            {
                Source = new Uri("avares://Kitbash.Ui/Themes/KitbashComfortable.axaml"),
            });
        }

        window.Show();
        window.UpdateLayout();

        return (window, row);
    }

    private static IReadOnlyList<RadioButton> Options(Segmented row) =>
        [.. row.GetVisualDescendants().OfType<RadioButton>()];

    private static Border Thumb(Segmented row) =>
        row.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Thumb");

    /// <summary>
    /// What the row leaves an option: its own height less the border and the padding it
    /// draws them inside. Read off the row rather than written down, since both are tokens.
    /// </summary>
    private static double Room(Segmented row) =>
        row.Bounds.Height
        - row.BorderThickness.Top - row.BorderThickness.Bottom
        - row.Padding.Top - row.Padding.Bottom;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOptionFillsTheRowAtEitherDensity(bool comfortable)
    {
        var (_, row) = Shown(comfortable);

        Assert.All(Options(row), option => Assert.Equal(Room(row), option.Bounds.Height, 1));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheCompactRowFillsTheSameWay(bool comfortable)
    {
        var (_, row) = Shown(comfortable, compact: true);

        Assert.All(Options(row), option => Assert.Equal(Room(row), option.Bounds.Height, 1));
    }

    // The thumb is sized from the option it stands on, so an option that did not fill the row
    // left the thumb short of it as well.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheThumbCoversTheOptionItStandsOn(bool comfortable)
    {
        var (_, row) = Shown(comfortable);

        var chosen = Options(row).Single(o => o.IsChecked == true);
        var thumb = Thumb(row);

        Assert.True(thumb.IsVisible);
        Assert.Equal(chosen.Bounds.Height, thumb.Height, 1);
        Assert.Equal(chosen.Bounds.Width, thumb.Width, 1);
    }

    // The two densities are different heights, or the row is not reading the token at all and
    // the tests above would pass over a control that never moved.
    [AvaloniaFact]
    public void TheTwoDensitiesAreNotTheSameHeight()
    {
        var (_, dense) = Shown(comfortable: false);
        var (_, comfortable) = Shown(comfortable: true);

        Assert.True(comfortable.Bounds.Height > dense.Bounds.Height);
    }
}
