using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// A split button clicked with Shift held runs the second command and not its own, through
/// real pointer input on a real window.
/// </summary>
public sealed class ShiftClickTests
{
    private int _own;
    private int _shifted;
    private bool _shiftedCanRun = true;

    private (Window Window, Point Primary) Show()
    {
        var split = new SplitButton
        {
            Content = "Open Project",
            Command = new RelayCommand(() => _own++),
            Flyout = new MenuFlyout(),
        };

        ShiftClick.SetCommand(split, new RelayCommand(() => _shifted++, () => _shiftedCanRun));

        var window = new Window { Width = 300, Height = 100, Content = split };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        // The left of the button is its main half. The right is the caret that opens the menu.
        var primary = split.TranslatePoint(new Point(10, split.Bounds.Height / 2), window)!.Value;

        return (window, primary);
    }

    private static void Click(Window window, Point at, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseDown(at, MouseButton.Left, modifiers);
        window.MouseUp(at, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void APlainClickRunsTheButtonsOwnCommand()
    {
        var (window, primary) = Show();

        Click(window, primary);

        Assert.Equal(1, _own);
        Assert.Equal(0, _shifted);
    }

    [AvaloniaFact]
    public void AShiftClickRunsTheSecondCommandInstead()
    {
        var (window, primary) = Show();

        Click(window, primary, RawInputModifiers.Shift);

        Assert.Equal(0, _own);
        Assert.Equal(1, _shifted);
    }

    /// <summary>Shift is read per click, so one shifted click does not change the next.</summary>
    [AvaloniaFact]
    public void TheClickAfterAShiftClickIsPlainAgain()
    {
        var (window, primary) = Show();

        Click(window, primary, RawInputModifiers.Shift);
        Click(window, primary);

        Assert.Equal(1, _own);
        Assert.Equal(1, _shifted);
    }

    [AvaloniaFact]
    public void ASecondCommandThatCannotRunLeavesTheClickAlone()
    {
        _shiftedCanRun = false;
        var (window, primary) = Show();

        Click(window, primary, RawInputModifiers.Shift);

        Assert.Equal(1, _own);
        Assert.Equal(0, _shifted);
    }
}
