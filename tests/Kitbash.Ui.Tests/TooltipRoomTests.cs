using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A popup window is sized to what it holds, so a tooltip's shadow needs room inside that
/// window or it is clipped square. Read off a real open tooltip.
/// </summary>
public class TooltipRoomTests
{
    private static (Popup Popup, Control Room, Control Card) Opened(
        PlacementMode placement = PlacementMode.Pointer)
    {
        var button = new Button { Content = "Hover me", Width = 120, Height = 26 };

        ToolTip.SetTip(button, "Run all validators");
        ToolTip.SetPlacement(button, placement);

        var window = new Window { Width = 400, Height = 200, Content = button };

        window.Show();
        ToolTip.SetIsOpen(button, true);

        var tip = Assert.IsType<ToolTip>(button.GetValue(ToolTipDiagnostics.ToolTipProperty));
        var borders = ((Visual)tip).GetVisualDescendants().OfType<Border>().Take(2).ToList();

        return (Assert.IsType<Popup>(tip.Parent), borders[0], borders[1]);
    }

    /// <summary>The card is what a person sees, and the room is not part of it.</summary>
    [AvaloniaFact]
    public void TheRoomIsAroundTheCardRatherThanInsideIt()
    {
        var (_, room, card) = Opened();

        Assert.True(card.Bounds.Width < room.Bounds.Width);
        Assert.Equal(22, card.Bounds.Height);
    }

    /// <summary>
    /// A tooltip opens 20px under the pointer, so room above it would put the popup window
    /// back over the pointer and take the hover and the click with it.
    /// </summary>
    [AvaloniaFact]
    public void ThereIsNoRoomAboveTheCard()
    {
        var (_, _, card) = Opened();

        Assert.Equal(0, card.Bounds.Y);
    }

    /// <summary>So the tip is drawn where it was drawn before it had any room at all.</summary>
    [AvaloniaFact]
    public void ThePullCancelsTheRoom()
    {
        var (popup, _, card) = Opened();

        Assert.Equal(0, card.Bounds.X + popup.HorizontalOffset);
        Assert.Equal(20, popup.VerticalOffset);
    }

    /// <summary>
    /// A rail item's tip is placed beside it, and the popup window starts at the item's right
    /// edge. Room to the left of the card would be window over the item, which takes the
    /// pointer, drops the hover and closes the tip.
    /// </summary>
    [AvaloniaFact]
    public void BesideAControlNothingReachesBackOverIt()
    {
        var (popup, room, card) = Opened(PlacementMode.Right);

        Assert.Equal(0, popup.HorizontalOffset);
        Assert.Equal(8, card.Bounds.X);
        Assert.Equal(8, room.Bounds.Width - card.Bounds.Width - 28);
    }

    /// <summary>
    /// The popup is centred on the control as a whole, room and all, so the deeper room under
    /// the card would leave the card riding high.
    /// </summary>
    [AvaloniaFact]
    public void BesideAControlTheCardIsCentredOnIt()
    {
        var (popup, room, card) = Opened(PlacementMode.Right);

        Assert.Equal(room.Bounds.Height / 2, card.Bounds.Y + card.Bounds.Height / 2 + popup.VerticalOffset);
    }

    /// <summary>The tip has to be beside the control on the second hover too.</summary>
    [AvaloniaFact]
    public void BesideAControlItSurvivesBeingReopened()
    {
        var (popup, _, card) = Opened(PlacementMode.Right);
        var button = Assert.IsType<Button>(popup.PlacementTarget);

        ToolTip.SetIsOpen(button, false);
        ToolTip.SetIsOpen(button, true);

        Assert.Equal(0, popup.HorizontalOffset);
        Assert.Equal(8, card.Bounds.X);
    }
}
