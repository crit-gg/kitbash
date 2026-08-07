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
    private static (Popup Popup, Control Room, Control Card) Opened()
    {
        var button = new Button { Content = "Hover me", Width = 120, Height = 26 };

        ToolTip.SetTip(button, "Run all validators");

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
}
