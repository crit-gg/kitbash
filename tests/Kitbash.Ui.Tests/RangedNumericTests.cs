using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The wash behind a bounded number. It is read off a rendered field rather than off the
/// converter, since a width that never reaches the template is a wash nobody sees.
/// </summary>
public class RangedNumericTests
{
    private const double Wide = 200;

    [AvaloniaTheory]
    [InlineData(0, 0)]
    [InlineData(1, 0.25)]
    [InlineData(2, 0.5)]
    [InlineData(4, 1)]
    public void TheWashIsHowFarAlongTheRangeTheValueHasCome(double value, double part)
    {
        var (window, field) = Field(value, 0, 4);

        try
        {
            var track = Part(field, "PART_Track");

            Assert.Equal(track.Bounds.Width * part, Part(field, "PART_Fill").Bounds.Width, 1);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A value outside its own ends is held at them rather than drawn past them.</summary>
    [AvaloniaTheory]
    [InlineData(-10, 0)]
    [InlineData(40, 1)]
    public void AValuePastAnEndStopsThere(double value, double part)
    {
        var (window, field) = Field(value, 0, 4);

        try
        {
            Assert.Equal(Part(field, "PART_Track").Bounds.Width * part, Part(field, "PART_Fill").Bounds.Width, 1);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Two ends that meet describe no range, so there is nothing to draw.</summary>
    [AvaloniaFact]
    public void ARangeOfNothingDrawsNothing()
    {
        var (window, field) = Field(3, 3, 3);

        try
        {
            Assert.Equal(0, Part(field, "PART_Fill").Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    private static Border Part(NumericUpDown field, string name) =>
        field.GetVisualDescendants().OfType<Border>().Single(border => border.Name == name);

    private static (Window Window, NumericUpDown Field) Field(double value, double low, double high)
    {
        var field = new NumericUpDown
        {
            Minimum = (decimal)low,
            Maximum = (decimal)high,
            Value = (decimal)value,
            Width = Wide,
            Theme = (Avalonia.Styling.ControlTheme)Avalonia.Application.Current!
                .FindResource("RangedNumeric")!,
        };

        var window = new Window { Width = 400, Height = 200, Content = field };

        window.Show();
        window.UpdateLayout();

        return (window, field);
    }
}
