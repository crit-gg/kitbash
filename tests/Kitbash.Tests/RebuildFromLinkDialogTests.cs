using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Kitbash.Core.Workspaces;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>The question a rebuild link asks, and the copy link menu beside it.</summary>
public sealed class RebuildFromLinkDialogTests
{
    private static RebuildFromLinkDialog Open(params string[] roots)
    {
        var model = new RebuildFromLinkViewModel(
            [.. roots.Select(root => new WorkspaceViewModel(
                new Workspace(root, "Slopworks", Exists: true, HasRepository: true), isCurrent: false, root))]);
        var dialog = new RebuildFromLinkDialog { DataContext = model };

        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        return dialog;
    }

    [AvaloniaFact]
    public void OneMatchNeedsNoChoice()
    {
        var dialog = Open("/work/slopworks");

        Assert.False(dialog.GetControl<ComboBox>("TargetChoice").IsVisible);
        Assert.True(dialog.GetControl<Button>("RebuildButton").IsEnabled);
        Assert.Equal("Rebuild Slopworks?", dialog.GetControl<TextBlock>("Heading").Text);
    }

    [AvaloniaFact]
    public void SeveralMatchesWaitForAChoice()
    {
        var dialog = Open("/work/slopworks", "/other/slopworks");
        var choice = dialog.GetControl<ComboBox>("TargetChoice");

        Assert.True(choice.IsVisible);
        Assert.Null(choice.SelectedItem);
        Assert.False(dialog.GetControl<Button>("RebuildButton").IsEnabled);

        choice.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.True(dialog.GetControl<Button>("RebuildButton").IsEnabled);
        Assert.Equal("/other/slopworks", dialog.Model?.Target?.Workspace.Root);
    }

    /// <summary>The submenu sits on the workspace page strip only. Its visibility is bound to the view model, which this window has none of.</summary>
    [AvaloniaFact]
    public void TheCopyLinkMenuHasEveryAction()
    {
        var window = new LauncherWindow();
        var menu = window.GetLogicalDescendants()
            .OfType<SplitButton>()
            .Select(split => split.Flyout)
            .OfType<MenuFlyout>()
            .SelectMany(flyout => flyout.Items.OfType<MenuItem>())
            .Single(item => item.Name == "CopyWorkspaceLink");

        Assert.Equal(
            ["Open workspace", "Open in editor", "Open without building", "Play", "Clean rebuild"],
            menu.Items.Cast<MenuItem>().Select(item => item.Header));
    }
}
