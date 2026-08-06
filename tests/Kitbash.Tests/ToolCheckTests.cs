using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// Check for updates, which is the only way to get past a cached release list, so a page
/// with nothing on it has to carry it too.
/// </summary>
public sealed class ToolCheckTests
{
    /// <summary>
    /// The empty state's own button, read off the real window rather than off the markup.
    /// </summary>
    [AvaloniaFact]
    public void TheEmptyStateCarriesTheCheck()
    {
        var window = new LauncherWindow();

        var check = Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            button => button.Name == "EmptyToolsCheck");

        var row = Assert.IsType<StackPanel>(check.Parent);

        // Beside the other way in rather than instead of it.
        Assert.Contains(
            row.Children.OfType<Button>(),
            button => button.GetLogicalDescendants().OfType<TextBlock>()
                .Any(block => block.Text == "Install from folder"));

        Assert.Contains(
            check.GetLogicalDescendants().OfType<Icon>(),
            icon => icon.Glyph == IconGlyph.RefreshCw);
    }

    /// <summary>
    /// The groups are rebuilt on the first pass of a refresh, so a check owned by a group
    /// would lose its own label the moment it was pressed.
    /// </summary>
    [Fact]
    public void TheCheckSurvivesTheGroupsItIsDrawnOn()
    {
        var check = new ToolCheckViewModel(() => Task.CompletedTask);
        var first = new ToolGroupViewModel("INSTALLED TOOLS", [], offersUpdates: false, check);
        var second = new ToolGroupViewModel("SCRIPTS", [], offersUpdates: false, check);

        Assert.Same(check, first.Check);
        Assert.Same(check, second.Check);
    }

    /// <summary>A slow action reports on itself in place, which here is the word.</summary>
    [Fact]
    public async Task TheLabelSaysSoWhileItRuns()
    {
        var running = new TaskCompletionSource();
        var check = new ToolCheckViewModel(() => running.Task);

        Assert.Equal("Check for updates", check.Label);

        var pressed = check.CheckCommand.ExecuteAsync(null);

        Assert.True(check.IsChecking);
        Assert.Equal("Checking", check.Label);

        running.SetResult();

        await pressed;

        Assert.False(check.IsChecking);
        Assert.Equal("Check for updates", check.Label);
    }
}
