using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a window says about its own shadow gutter. The numbers are the window's padding in
/// device pixels, so a frame that gives the padding up when it is maximized declares nothing
/// without anybody wiring the two together.
/// </summary>
public class WindowShadowTests
{
    /// <summary>A desktop that writes down what it was told instead of telling anybody.</summary>
    private sealed class Heard : IWindowShadow
    {
        public List<WindowShadowExtents> Said { get; } = [];

        public List<string> Kinds { get; } = [];

        public bool CanDeclare => true;

        public void Declare(nint window, string kind, WindowShadowExtents extents)
        {
            Kinds.Add(kind);
            Said.Add(extents);
        }
    }

    private static ChromelessWindow Open(Heard heard, WindowChromeKind chrome = WindowChromeKind.Drawn)
    {
        var window = new ChromelessWindow
        {
            Width = 964,
            Height = 724,
            Chrome = chrome,
            Shadow = heard,
        };

        window.Show();

        return window;
    }

    [AvaloniaFact]
    public void ADrawnWindowDeclaresItsGutter()
    {
        var heard = new Heard();

        Open(heard);

        Assert.Equal(new WindowShadowExtents(12, 12, 12, 12), heard.Said[^1]);
    }

    /// <summary>The frame drops its padding when maximized, so the declaration follows it.</summary>
    [AvaloniaFact]
    public void MaximizingGivesTheGutterUp()
    {
        var heard = new Heard();
        var window = Open(heard);

        window.WindowState = WindowState.Maximized;

        Assert.Equal(WindowShadowExtents.None, heard.Said[^1]);
    }

    [AvaloniaFact]
    public void RestoringTakesTheGutterBack()
    {
        var heard = new Heard();
        var window = Open(heard);

        window.WindowState = WindowState.Maximized;
        window.WindowState = WindowState.Normal;

        Assert.Equal(new WindowShadowExtents(12, 12, 12, 12), heard.Said[^1]);
    }

    /// <summary>The desktop draws that frame, so there is no gutter of ours to describe.</summary>
    [AvaloniaFact]
    public void AWindowTheDesktopFramesDeclaresNothing()
    {
        var heard = new Heard();

        Open(heard, WindowChromeKind.Desktop);

        Assert.DoesNotContain(heard.Said, extents => extents != WindowShadowExtents.None);
    }

    /// <summary>
    /// A dialog is built by whoever opens it and is never handed the launcher's services,
    /// so it takes this from its owner the way it takes the frame.
    /// </summary>
    [AvaloniaFact]
    public void ADialogTakesItsOwnersDeclaration()
    {
        var heard = new Heard();
        var owner = Open(heard);

        var dialog = new ChromelessWindow { Width = 400, Height = 300 };

        dialog.Show(owner);

        Assert.Same(heard, dialog.Shadow);
        Assert.Equal(new WindowShadowExtents(12, 12, 12, 12), heard.Said[^1]);
    }

    /// <summary>A property write costs a round trip, so the same answer is not sent twice.</summary>
    [AvaloniaFact]
    public void TheSameGutterIsNotDeclaredTwice()
    {
        var heard = new Heard();
        var window = Open(heard);

        var said = heard.Said.Count;

        window.Padding = window.Padding;

        Assert.Equal(said, heard.Said.Count);
    }
}
