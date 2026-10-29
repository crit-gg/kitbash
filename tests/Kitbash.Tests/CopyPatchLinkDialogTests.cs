using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>The dialog that makes a patch link, and the menu item that opens it.</summary>
public sealed class CopyPatchLinkDialogTests
{
    private const string Commit = "3f9c2a1e8b7d6c5f4a3b2c1d0e9f8a7b6c5d4e3f";

    private static (CopyPatchLinkDialog Dialog, CopyPatchLinkViewModel Model) Open(string? linkName = "Slopworks")
    {
        var model = new CopyPatchLinkViewModel("My slopworks", linkName);
        var dialog = new CopyPatchLinkDialog { DataContext = model };

        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        return (dialog, model);
    }

    private static void Type(CopyPatchLinkViewModel model, string address)
    {
        model.Address = address;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AnHttpsAddressMakesALinkThatReadsBack()
    {
        var (dialog, model) = Open();

        Type(model, "https://drop.crit.gg/downloadFile?id=gxnF0Eu79YQitss");

        var text = dialog.GetControl<SelectableTextBlock>("LinkText").Text;

        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal("https://drop.crit.gg/downloadFile?id=gxnF0Eu79YQitss", link.Value("url"));
        Assert.Equal("Slopworks", link.Value("workspace"));
        Assert.True(dialog.GetControl<Button>("CopyButton").IsEnabled);
    }

    [AvaloniaFact]
    public void PlainHttpIsRefused()
    {
        var (dialog, model) = Open();

        Type(model, "http://drop.crit.gg/fix.patch");

        Assert.Equal("Use an https address.", model.AddressProblem);
        Assert.Contains("error", dialog.GetControl<TextBox>("AddressBox").Classes);
        Assert.False(dialog.GetControl<Button>("CopyButton").IsEnabled);
    }

    [AvaloniaFact]
    public void ACommitNameSaysWhereItCameFrom()
    {
        var (dialog, _) = Open(Commit);

        Assert.True(dialog.GetControl<TextBlock>("CommitNote").IsVisible);
    }

    [AvaloniaFact]
    public void ANameIsNotACommit()
    {
        var (dialog, _) = Open();

        Assert.False(dialog.GetControl<TextBlock>("CommitNote").IsVisible);
        Assert.False(dialog.GetControl<Alert>("NoName").IsOpen);
    }

    [AvaloniaFact]
    public void NothingToNameItByBlocksTheCopy()
    {
        var (dialog, model) = Open(linkName: null);

        Type(model, "https://drop.crit.gg/fix.patch");

        Assert.True(dialog.GetControl<Alert>("NoName").IsOpen);
        Assert.False(dialog.GetControl<Button>("CopyButton").IsEnabled);
    }

    /// <summary>The item sits under Apply patch. Its visibility is bound to the view model, which this window has none of.</summary>
    [AvaloniaFact]
    public void TheMenuItemIsInTheGitMenu()
    {
        var window = new LauncherWindow();
        var button = window.GetLogicalDescendants().OfType<SplitButton>().Single(split => split.Name == "GitButton");
        var items = Assert.IsType<MenuFlyout>(button.Flyout).Items.Cast<MenuItem>().ToList();

        Assert.Equal(["Apply patch", "Copy patch link"], items.Select(item => item.Header));
    }
}
