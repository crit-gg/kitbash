using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// One thing failed and here is what it said. Nothing in the app opens this often, so it is
/// tested rather than trusted.
/// </summary>
public class ErrorDialogTests
{
    [AvaloniaFact]
    public void TheProgramsOwnWordsAreShownAndAreCopyable()
    {
        var dialog = ErrorDialog.For("Push", "That push was refused.", "fatal: no upstream");

        Assert.Equal("Push", dialog.Title);
        Assert.Equal("That push was refused.", dialog.GetControl<TextBlock>("Heading").Text);
        Assert.Equal("fatal: no upstream", dialog.GetControl<SelectableTextBlock>("Details").Text);
        Assert.True(dialog.GetControl<Button>("Copy").IsVisible);
    }

    // A failure git said nothing about still opens, with the well and the copy button gone
    // rather than empty.
    [AvaloniaFact]
    public void NoDetailsLeavesNothingToCopy()
    {
        var dialog = ErrorDialog.For("Push", "That push was refused.", "");

        Assert.False(dialog.GetControl<Border>("Well").IsVisible);
        Assert.False(dialog.GetControl<Button>("Copy").IsVisible);
    }
}
