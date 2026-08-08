using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;
using Kitbash.Workspaces;

namespace Kitbash.Tests;

/// <summary>
/// The links section, drawn out of the window's own template, so what is checked is the
/// markup rather than a copy of it made here.
/// </summary>
public sealed class WorkspaceLinkRowTests
{
    /// <summary>
    /// The name is the whole row. The address is on the row rather than in it, since a
    /// workspace names its own links and the URL would only repeat the label.
    /// </summary>
    [AvaloniaFact]
    public void TheRowIsTheNameAndTwoMarks()
    {
        var row = Row(Link("Design docs", "https://example.com/design", IconGlyph.File));

        var label = Assert.Single(
            row.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "Design docs");

        Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);

        // The workspace's own mark first, then the one saying the click leaves Kitbash.
        Assert.Equal(
            [IconGlyph.File, IconGlyph.Link],
            row.GetLogicalDescendants().OfType<Icon>().Select(icon => icon.Glyph));

        Assert.Equal("https://example.com/design", ToolTip.GetTip(row));
    }

    /// <summary>Pressing it hands the address to the desktop and nothing else.</summary>
    [AvaloniaFact]
    public void PressingARowOpensItsAddress()
    {
        List<WorkspaceLink> opened = [];
        var link = Link("Issue tracker", "https://example.com/issues", IconGlyph.AlertCircle);

        Row(link, opened.Add).Command!.Execute(null);

        Assert.Same(link, Assert.Single(opened));
    }

    /// <summary>
    /// A link is not a tool, so the row carries no filled button and rests on the same
    /// values every secondary button does.
    /// </summary>
    [AvaloniaFact]
    public void TheRowIsQuieterThanACard()
    {
        var row = Row(Link("Repository", "https://example.com/repo", IconGlyph.GitBranch));

        Assert.DoesNotContain("primary", row.Classes);
        Assert.DoesNotContain("neutral", row.Classes);

        // No pinned height, so the row is its content and its padding at either density.
        Assert.True(double.IsNaN(row.Height));
        Assert.Equal(32, row.Bounds.Height);
    }

    /// <summary>
    /// The links are above the tools and in a row of their own, so the empty state under
    /// them can never be drawn over a link.
    /// </summary>
    [AvaloniaFact]
    public void TheLinksAreAboveTheToolsRatherThanOverThem()
    {
        var launcher = new LauncherWindow();

        var links = Assert.Single(
            launcher.GetLogicalDescendants().OfType<StackPanel>(),
            panel => panel.Name == "WorkspaceLinks");

        var empty = Assert.Single(
            launcher.GetLogicalDescendants().OfType<Button>(),
            button => button.Name == "EmptyToolsCheck");

        var page = Assert.IsType<Grid>(links.Parent);
        var tools = Assert.Single(page.Children.OfType<Panel>(), child => child != links);

        Assert.Equal(0, Grid.GetRow(links));
        Assert.Equal(1, Grid.GetRow(tools));

        // The empty state and the tool cards are both inside that second row.
        Assert.Contains(empty, tools.GetLogicalDescendants());
        Assert.Contains(
            tools.GetLogicalDescendants().OfType<ItemsControl>(),
            items => items.Name == "ToolGroups");
    }

    private static WorkspaceLink Link(string label, string url, IconGlyph icon) =>
        new(label, WebAddress.Parse(url), icon);

    /// <summary>One row, built from the markup's own template.</summary>
    private static Button Row(WorkspaceLink link, Action<WorkspaceLink>? open = null)
    {
        var launcher = new LauncherWindow();

        var links = Assert.Single(
            launcher.GetLogicalDescendants().OfType<StackPanel>()
                .Where(panel => panel.Name == "WorkspaceLinks")
                .SelectMany(panel => panel.GetLogicalDescendants().OfType<ItemsControl>()));

        var model = new WorkspaceLinkViewModel(link, open ?? (_ => { }));
        var built = Assert.IsAssignableFrom<Control>(links.ItemTemplate!.Build(model));

        built.DataContext = model;

        // Inside a panel that gives a child its own height, the way the grid of rows does.
        // As the window's own content it would stretch to the whole page.
        launcher.Content = new StackPanel { Children = { built } };

        launcher.Show();

        Dispatcher.UIThread.RunJobs();

        launcher.UpdateLayout();

        return Assert.IsType<Button>(built);
    }
}
