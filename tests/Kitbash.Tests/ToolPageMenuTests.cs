using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The page has no bar of its own, so its rare actions ride on the first heading in a menu
/// rather than beside the buttons a person came to press.
/// </summary>
public sealed class ToolPageMenuTests
{
    [AvaloniaFact]
    public void TheFirstHeadingCarriesTheMenu()
    {
        var button = Assert.Single(
            Heading(pageMenu: true).GetLogicalDescendants().OfType<Button>(),
            candidate => candidate.Flyout is MenuFlyout);

        Assert.True(button.IsVisible);
        Assert.Contains(
            button.GetLogicalDescendants().OfType<Icon>(),
            icon => icon.Glyph == IconGlyph.DotsVerticalRounded);

        // The button is at the end of the row and the menu is far wider than it.
        Assert.True(Popups.GetAlignsRight(button));
    }

    /// <summary>Installing from a folder is in there rather than on the row.</summary>
    [AvaloniaFact]
    public void InstallFromFolderIsInTheMenu()
    {
        var heading = Heading(pageMenu: true);

        Assert.DoesNotContain(
            heading.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "Install from folder");

        var menu = Assert.Single(
            heading.GetLogicalDescendants().OfType<Button>()
                .Select(button => button.Flyout)
                .OfType<MenuFlyout>());

        var item = Assert.IsType<MenuItem>(Assert.Single(menu.Items));

        Assert.Equal("Install from folder", item.Header);
    }

    /// <summary>Every other heading is the group's name and nothing else.</summary>
    [AvaloniaFact]
    public void ASecondHeadingCarriesNone()
    {
        Assert.DoesNotContain(
            Heading(pageMenu: false).GetLogicalDescendants().OfType<Button>(),
            button => button.Flyout is MenuFlyout && button.IsVisible);
    }

    /// <summary>
    /// The group's own row, drawn out of the window's own template, so what is checked is
    /// the markup rather than a copy of it made here.
    /// </summary>
    private static Control Heading(bool pageMenu)
    {
        var launcher = new LauncherWindow();

        var groups = Assert.Single(
            launcher.GetLogicalDescendants().OfType<ItemsControl>(),
            items => items.Name == "ToolGroups");

        var group = new ToolGroupViewModel("INSTALLED TOOLS", [], offersUpdates: false)
        {
            ShowsPageMenu = pageMenu,
        };

        var built = Assert.IsAssignableFrom<Control>(groups.ItemTemplate!.Build(group));

        built.DataContext = group;
        launcher.Content = built;

        launcher.Show();

        Dispatcher.UIThread.RunJobs();

        launcher.UpdateLayout();

        return built;
    }
}
