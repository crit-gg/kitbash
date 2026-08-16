using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The three window frames. Drawn is Kitbash's own, Desktop hands the whole frame over, and
/// Overlay hands it over but keeps the client area under it, so the desktop's caption
/// buttons land on the Kitbash title bar. macOS is what the third exists for.
/// </summary>
public class WindowChromeTests
{
    private static ChromelessWindow Open(WindowChromeKind chrome, double width = 964)
    {
        var window = new ChromelessWindow
        {
            Width = width,
            Height = 724,
            MinWidth = 844,
            Content = new WindowTitleBar { Title = "Kitbash" },
            Chrome = chrome,
        };

        window.Show();

        return window;
    }

    private static WindowTitleBar Bar(ChromelessWindow window) =>
        window.GetVisualDescendants().OfType<WindowTitleBar>().First();

    [AvaloniaTheory]
    [InlineData(WindowChromeKind.Drawn, "chromeless")]
    [InlineData(WindowChromeKind.Desktop, "nativeChrome")]
    [InlineData(WindowChromeKind.Overlay, "overlayChrome")]
    public void EachModeSetsItsOwnClassAndNoOther(WindowChromeKind chrome, string expected)
    {
        var window = Open(chrome);

        string[] all = ["chromeless", "nativeChrome", "overlayChrome"];

        Assert.Equal(expected, Assert.Single(all, window.Classes.Contains));
    }

    /// <summary>Drawn is the default, so a window that never assigns one still has a frame.</summary>
    [AvaloniaFact]
    public void AWindowThatSaysNothingIsDrawn()
    {
        var window = new ChromelessWindow();

        window.Show();

        Assert.Equal(WindowChromeKind.Drawn, window.Chrome);
        Assert.Contains("chromeless", window.Classes);
    }

    /// <summary>
    /// The macOS backend zeroes its extended margins and hides the caption buttons unless
    /// the decorations are Full, so Overlay must not ask for anything else.
    /// </summary>
    [AvaloniaFact]
    public void OverlayExtendsTheClientAreaAndKeepsFullDecorations()
    {
        var window = Open(WindowChromeKind.Overlay);

        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
    }

    [AvaloniaFact]
    public void DrawnTakesTheWholeFrameOverItself()
    {
        var window = Open(WindowChromeKind.Drawn);

        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
    }

    /// <summary>
    /// Every window asks for its design size plus the gutter, since the drawn frame sits
    /// inside the window's own padding. A mode with no frame has no gutter to pay for.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(WindowChromeKind.Drawn, 964, 844)]
    [InlineData(WindowChromeKind.Desktop, 940, 820)]
    [InlineData(WindowChromeKind.Overlay, 940, 820)]
    public void TheGutterComesOutOfTheSizeWhenNothingDrawsIt(
        WindowChromeKind chrome,
        double width,
        double minimum)
    {
        var window = Open(chrome);

        Assert.Equal(width, window.Width);
        Assert.Equal(minimum, window.MinWidth);
    }

    /// <summary>A dialog sizes to its content, so there is no declared height to correct.</summary>
    [AvaloniaFact]
    public void AWindowSizingToItsContentIsLeftAlone()
    {
        var window = new ChromelessWindow
        {
            Width = 484,
            SizeToContent = SizeToContent.Height,
            Chrome = WindowChromeKind.Overlay,
        };

        window.Show();

        Assert.Equal(460, window.Width);
        Assert.True(double.IsNaN(window.Height) || window.Height > 0);
    }

    /// <summary>
    /// Desktop chrome draws a title bar of its own, so a row carrying nothing else says
    /// nothing. Overlay has no other title bar, so the row is the title bar.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(WindowChromeKind.Drawn, true)]
    [InlineData(WindowChromeKind.Desktop, false)]
    [InlineData(WindowChromeKind.Overlay, true)]
    public void OnlyDesktopChromeCanHideTheRow(WindowChromeKind chrome, bool visible)
    {
        var window = Open(chrome);

        Assert.Equal(visible, Bar(window).IsVisible);
    }

    /// <summary>
    /// The desktop supplies its own under Overlay, so ours would be a pair. Desktop is not
    /// asked, since it hides the whole row and never realises the template to look in.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(WindowChromeKind.Drawn, true)]
    [InlineData(WindowChromeKind.Overlay, false)]
    public void OnlyDrawnKeepsTheKitbashCaptionButtons(WindowChromeKind chrome, bool visible)
    {
        var window = Open(chrome);

        var buttons = Bar(window)
            .GetVisualDescendants()
            .OfType<StackPanel>()
            .First(panel => panel.Name == "PART_CaptionButtons");

        Assert.Equal(visible, buttons.IsVisible);
    }

    /// <summary>
    /// Measured on macOS 26.4: the caption buttons are 14 wide on 23 centres from x=9, so
    /// the last ends at 69 and the row starts 11 after it, which is the gap the drawn frame
    /// leaves at its own edge. Desktop is not asked, since it hides the whole row.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(WindowChromeKind.Drawn, 11)]
    [InlineData(WindowChromeKind.Overlay, 80)]
    public void TheRowStartsAfterWhateverTheDesktopDrew(WindowChromeKind chrome, double left)
    {
        var window = Open(chrome);

        var row = Bar(window)
            .GetVisualDescendants()
            .OfType<Grid>()
            .First(grid => grid.Name == "PART_Row");

        Assert.Equal(left, row.Margin.Left);
    }

    /// <summary>Full screen moves them out of the window, so the inset would be a gap.</summary>
    [AvaloniaFact]
    public void FullScreenPutsTheRowBack()
    {
        var window = Open(WindowChromeKind.Overlay);

        window.WindowState = WindowState.FullScreen;

        var row = Bar(window)
            .GetVisualDescendants()
            .OfType<Grid>()
            .First(grid => grid.Name == "PART_Row");

        Assert.Equal(11, row.Margin.Left);
    }

    /// <summary>
    /// A dialog is opened for its owner so it wears the same frame. Reading Owner would be
    /// too late, since a window is given one after its first layout pass.
    /// </summary>
    [AvaloniaFact]
    public void ADialogWearsWhatTheWindowThatOpenedItWears()
    {
        var owner = Open(WindowChromeKind.Overlay);

        var dialog = new DialogWindow { Width = 484, Height = 300 };

        // Not awaited, since nothing answers it here. What matters is what it was given.
        _ = dialog.ShowFor<bool>(owner);

        Assert.Equal(WindowChromeKind.Overlay, dialog.Chrome);

        dialog.Close();
    }
}
