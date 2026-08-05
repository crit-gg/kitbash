using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The real window, built headlessly, so where the button sits is read off the control
/// tree rather than off the markup.
/// </summary>
public sealed class EngineStripTests
{
    private static Button OpenIn(LauncherWindow window) =>
        Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            button => button.Name == "OpenInButton");

    [AvaloniaFact]
    public void TheButtonSitsRightOfTheSplitButton()
    {
        var window = new LauncherWindow();
        var button = OpenIn(window);

        var strip = Assert.IsType<Grid>(button.Parent);
        var split = Assert.Single(strip.Children.OfType<SplitButton>());

        Assert.Equal(3, strip.ColumnDefinitions.Count);
        Assert.Equal(1, Grid.GetColumn(split));
        Assert.Equal(2, Grid.GetColumn(button));
    }

    /// <summary>An icon button carries no word, so the theme cannot supply one.</summary>
    [AvaloniaFact]
    public void TheButtonIsNamedForSomebodyWhoCannotSeeIt()
    {
        var button = OpenIn(new LauncherWindow());

        Assert.Equal("Open in external tool", Avalonia.Automation.AutomationProperties.GetName(button));
        Assert.NotNull(ToolTip.GetTip(button));
    }

    /// <summary>
    /// Square, and the same height as the split button, so the two read as one row. The
    /// icon kind's own 24 is smaller than a control, which is what this overrides.
    /// </summary>
    [AvaloniaFact]
    public void TheButtonIsSquareAndAsTallAsTheSplitButton()
    {
        var window = new LauncherWindow();
        var button = OpenIn(window);
        var strip = Assert.IsType<Grid>(button.Parent);
        var split = Assert.Single(strip.Children.OfType<SplitButton>());

        window.Show();

        Assert.Equal(button.Bounds.Height, button.Bounds.Width);
        Assert.Equal(split.Bounds.Height, button.Bounds.Height);
        Assert.True(button.Bounds.Width > 0, "the button was never laid out");
    }

    [AvaloniaFact]
    public void TheButtonCarriesAMenuFlyout()
    {
        Assert.IsType<MenuFlyout>(OpenIn(new LauncherWindow()).Flyout);
    }
}
