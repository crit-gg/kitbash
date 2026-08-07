using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The page shown with no workspace offers the same ways in as the workspace dropdown,
/// read off the real control tree rather than off the markup.
/// </summary>
public sealed class NoWorkspacesPageTests
{
    private static IReadOnlyList<string> Labels(Panel panel) =>
        [.. panel.Children.OfType<Button>()
            .Select(button => Assert.Single(button.GetLogicalDescendants().OfType<TextBlock>()).Text ?? string.Empty)];

    private static Panel Actions(LauncherWindow window) =>
        Assert.Single(
            window.GetLogicalDescendants().OfType<Panel>(),
            panel => panel.Name == "NoWorkspacesActions");

    private static Panel MenuActions(LauncherWindow window)
    {
        var selector = Assert.Single(
            window.GetLogicalDescendants().OfType<DropDownButton>(),
            button => button.Name == "WorkspaceSelector");

        var flyout = Assert.IsType<Flyout>(selector.Flyout);
        var popover = Assert.IsType<Kitbash.Ui.Controls.Popover>(flyout.Content);

        return Assert.IsAssignableFrom<Panel>(popover.Footer);
    }

    [AvaloniaFact]
    public void ThePageOffersTheSameThreeActionsAsTheDropdown()
    {
        var window = new LauncherWindow();

        Assert.Equal(Labels(MenuActions(window)), Labels(Actions(window)));
    }

    /// <summary>Creating one is the lead, so it is the only filled button.</summary>
    [AvaloniaFact]
    public void CreatingIsThePrimaryAction()
    {
        var buttons = Actions(new LauncherWindow()).Children.OfType<Button>().ToList();

        Assert.Equal(3, buttons.Count);
        Assert.Contains("primary", buttons[0].Classes);
        Assert.DoesNotContain("primary", buttons[1].Classes);
        Assert.DoesNotContain("primary", buttons[2].Classes);
    }
}
