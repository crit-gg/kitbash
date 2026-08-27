using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A window that draws its own title bar. The frame lives in
/// Themes/Controls/WindowChrome.axaml and its resize grips are wired by
/// <see cref="WindowResize"/>.
/// </summary>
public class ChromelessWindow : Window
{
    /// <summary>
    /// Who draws the frame. Set from <c>Kitbash.Core.Settings.IWindowSettings.Chrome</c>
    /// when the window is built. It is not watched, so changing the setting takes effect
    /// on the next launch.
    /// </summary>
    public static readonly StyledProperty<WindowChromeKind> ChromeProperty =
        AvaloniaProperty.Register<ChromelessWindow, WindowChromeKind>(nameof(Chrome));

    /// <summary>
    /// The transparent room the drawn frame's shadow falls into, on every side. Held here
    /// as well as in the theme because a window's Width and Height include it.
    /// </summary>
    private const double ShadowGutter = 12;

    private readonly WindowShadow _shadow;

    public ChromelessWindow()
    {
        // Avalonia moves focus to the first focusable thing above whatever was pressed and
        // gives up when there is none, so the window taking focus is what drops a field's.
        Focusable = true;

        WindowResize.SetGrips(this, true);

        _shadow = new WindowShadow(this);

        Classes.Set("chromeless", true);
        Classes.Set("inactive", !IsActive);
    }

    /// <inheritdoc cref="ChromeProperty"/>
    public WindowChromeKind Chrome
    {
        get => GetValue(ChromeProperty);
        set => SetValue(ChromeProperty, value);
    }

    /// <summary>
    /// Tells the desktop which part of the window is the shadow gutter, so snapping and
    /// tiling measure the frame a person can see. Handed over when the window is built, the
    /// way every other service a window uses is, and taken from an owner that has one.
    /// Null declares nothing, which is what a headless window gets.
    /// </summary>
    public IWindowShadow? Shadow
    {
        get => _shadow.Service;
        set
        {
            _shadow.Service = value;
            _shadow.Declare();
        }
    }

    /// <summary>Whether Kitbash draws the frame, which is the only mode with a gutter.</summary>
    private bool IsDrawn => Chrome is WindowChromeKind.Drawn;

    /// <summary>Styles target Window, so a derived window keeps the same frame.</summary>
    protected override Type StyleKeyOverride => typeof(Window);

    /// <summary>
    /// The classes the frame selects on, one per mode and mutually exclusive.
    /// <c>chromeless</c> picks the drawn template and its resize grips, <c>nativeChrome</c>
    /// drops it because the desktop draws a title bar of its own, and <c>overlayChrome</c>
    /// drops it because the desktop draws its caption buttons over ours. <c>inactive</c>
    /// drops the chrome to the muted tier. Every control carries its own classes rather
    /// than a selector reaching across the window, so the title bar mirrors the same state.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChromeProperty)
        {
            var chrome = change.GetNewValue<WindowChromeKind>();

            Classes.Set("chromeless", chrome is WindowChromeKind.Drawn);
            Classes.Set("nativeChrome", chrome is WindowChromeKind.Desktop);
            Classes.Set("overlayChrome", chrome is WindowChromeKind.Overlay);

            TakeBackTheGutter(change.GetOldValue<WindowChromeKind>(), chrome);
        }
        else if (change.Property == IsActiveProperty)
        {
            Classes.Set("inactive", !change.GetNewValue<bool>());
        }
        else if (change.Property == PaddingProperty)
        {
            // The gutter is the padding, and the frame drops both when it is maximized, so
            // this is the one place either of them has to be watched.
            _shadow.Declare();
        }
    }

    /// <summary>
    /// A dialog is built by whoever opens it rather than by a container, so it takes this
    /// from its owner the same way it takes the frame.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Shadow is null && Owner is ChromelessWindow framed)
        {
            Shadow = framed.Shadow;
        }

        _shadow.Declare();
    }

    /// <summary>
    /// Every window asks for the size its design wants plus the gutter, since the frame
    /// sits inside the window's own Padding. A mode that draws no frame has no gutter, so
    /// the same numbers would make the window 24 wider and taller than it should be. The
    /// constructor has already run the XAML by the time Chrome is set, so this corrects
    /// what was declared rather than fighting it.
    /// </summary>
    private void TakeBackTheGutter(WindowChromeKind was, WindowChromeKind now)
    {
        var room = ((was is WindowChromeKind.Drawn ? 0 : 1) - (now is WindowChromeKind.Drawn ? 0 : 1))
                   * ShadowGutter * 2;

        if (room == 0)
        {
            return;
        }

        Width = Shift(Width, room);
        Height = Shift(Height, room);
        MinWidth = Shift(MinWidth, room);
        MinHeight = Shift(MinHeight, room);
    }

    // NaN is a window sizing to its content, which has no declared number to correct.
    private static double Shift(double value, double by) =>
        double.IsNaN(value) || value <= 0 ? value : Math.Max(0, value + by);

    /// <summary>
    /// Moves the window. The second click of a double click is ignored so it can
    /// reach <see cref="ToggleMaximizedFromTitleBar"/> instead. It does nothing when
    /// the desktop draws the frame, since the desktop's own title bar carries the
    /// gesture and ours is then ordinary content.
    /// </summary>
    protected internal void BeginMoveWindow(PointerPressedEventArgs e)
    {
        // Under either desktop drawn mode the platform owns the gesture. Overlay tags the
        // title bar with WindowDecorationProperties.ElementRole, so the move is the real
        // native one, and doing it here as well would start two drags from one press.
        if (!IsDrawn)
        {
            return;
        }

        if (e.ClickCount == 1 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected internal void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    /// <summary>
    /// The double click gesture on the title bar. It does nothing under either desktop
    /// drawn mode, since the platform already carries it there.
    /// </summary>
    protected internal void ToggleMaximizedFromTitleBar()
    {
        if (!IsDrawn)
        {
            return;
        }

        ToggleMaximized();
    }
}
