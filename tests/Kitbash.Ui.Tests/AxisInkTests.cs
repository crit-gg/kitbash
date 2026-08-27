using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The axis letter takes its ink from the axis it names. A view says which axis, never
/// which colour, so the four of them cannot drift apart across the tools.
/// </summary>
public class AxisInkTests
{
    [AvaloniaTheory]
    [InlineData(0, "AxisX")]
    [InlineData(1, "AxisY")]
    [InlineData(2, "AxisZ")]
    [InlineData(3, "AxisW")]
    public void EachAxisTakesItsOwnInk(int index, string token)
    {
        var (window, label) = Label(index);

        try
        {
            var wanted = Assert.IsAssignableFrom<ISolidColorBrush>(
                Avalonia.Application.Current!.FindResource(token));

            Assert.Equal(wanted.Color, Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A label naming no axis is left alone, so the styles reach nothing else.</summary>
    [AvaloniaFact]
    public void ALabelNamingNoAxisKeepsItsOwnInk()
    {
        var (window, label) = Label(Axis.None);

        try
        {
            var wanted = Assert.IsAssignableFrom<ISolidColorBrush>(
                Avalonia.Application.Current!.FindResource("AxisX"));

            Assert.NotEqual(wanted.Color, (label.Foreground as ISolidColorBrush)?.Color);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, TextBlock Label) Label(int index)
    {
        var label = new TextBlock { Text = "X", Classes = { "axis" } };

        Axis.SetIndex(label, index);

        var window = new Window { Width = 200, Height = 100, Content = label };

        window.Show();
        window.UpdateLayout();

        return (window, label);
    }
}
