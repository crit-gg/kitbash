using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Popups is written to force one alignment whatever a control asked for, so the opt out
/// is read off a real popup rather than off the property that was set.
/// </summary>
public sealed class PopupAlignmentTests
{
    private static (Window Window, Button Button) Opened(bool alignsRight)
    {
        var button = new Button { Content = "Open in", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        Popups.SetAlignsRight(button, alignsRight);

        var menu = new MenuFlyout();
        menu.Items.Add(new MenuItem { Header = "A rather long menu row" });
        button.Flyout = menu;

        var window = new Window { Width = 400, Height = 200, Content = button };
        window.Show();
        menu.ShowAt(button);

        return (window, button);
    }

    private static Popup PopupOf(Button button) =>
        Assert.IsType<Popup>(((MenuFlyout)button.Flyout!).Popup);

    [AvaloniaFact]
    public void LeftIsStillTheDefault()
    {
        var (_, button) = Opened(alignsRight: false);

        Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, PopupOf(button).Placement);
    }

    [AvaloniaFact]
    public void TheOptOutLinesUpWithTheRightEdge()
    {
        var (_, button) = Opened(alignsRight: true);

        Assert.Equal(PlacementMode.BottomEdgeAlignedRight, PopupOf(button).Placement);
    }

    /// <summary>
    /// The room pull is a leftward nudge, so lining up right needs it mirrored or the
    /// popup hangs a room's width off the control.
    /// </summary>
    [AvaloniaFact]
    public void TheRoomPullIsMirrored()
    {
        var (_, left) = Opened(alignsRight: false);
        var (_, right) = Opened(alignsRight: true);

        Assert.Equal(-PopupOf(left).HorizontalOffset, PopupOf(right).HorizontalOffset);
        Assert.NotEqual(0, PopupOf(left).HorizontalOffset);
    }

    /// <summary>The control rewrites placement on every open, so it has to survive a reopen.</summary>
    [AvaloniaFact]
    public void ItSurvivesBeingReopened()
    {
        var (_, button) = Opened(alignsRight: true);
        var menu = (MenuFlyout)button.Flyout!;

        menu.Hide();
        menu.ShowAt(button);

        Assert.Equal(PlacementMode.BottomEdgeAlignedRight, PopupOf(button).Placement);
    }
}
