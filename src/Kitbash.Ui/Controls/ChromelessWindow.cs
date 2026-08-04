using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A window that draws its own title bar. The frame lives in
/// Themes/Controls/WindowChrome.axaml and its resize grips are wired by
/// <see cref="WindowResize"/>.
/// </summary>
public class ChromelessWindow : Window
{
    /// <summary>
    /// Whether the desktop draws the frame. Set from
    /// <c>Kitbash.Core.Settings.IWindowSettings</c> when the window is built. It is
    /// not watched, so changing the setting takes effect on the next launch.
    /// </summary>
    public static readonly StyledProperty<bool> UsesNativeChromeProperty =
        AvaloniaProperty.Register<ChromelessWindow, bool>(nameof(UsesNativeChrome));

    public ChromelessWindow()
    {
        // Avalonia moves focus to the first focusable thing above whatever was pressed and
        // gives up when there is none, so the window taking focus is what drops a field's.
        Focusable = true;

        WindowResize.SetGrips(this, true);

        Classes.Set("chromeless", true);
        Classes.Set("inactive", !IsActive);
    }

    /// <inheritdoc cref="UsesNativeChromeProperty"/>
    public bool UsesNativeChrome
    {
        get => GetValue(UsesNativeChromeProperty);
        set => SetValue(UsesNativeChromeProperty, value);
    }

    /// <summary>Styles target Window, so a derived window keeps the same frame.</summary>
    protected override Type StyleKeyOverride => typeof(Window);

    /// <summary>
    /// The classes the frame selects on. <c>chromeless</c> picks the drawn template and
    /// its resize grips, <c>nativeChrome</c> drops it because the desktop supplies its
    /// own, and the two are mutually exclusive. <c>inactive</c> drops the chrome to the
    /// muted tier. Every control carries its own classes rather than a selector reaching
    /// across the window, so the title bar mirrors the same state onto itself.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UsesNativeChromeProperty)
        {
            var native = change.GetNewValue<bool>();

            Classes.Set("chromeless", !native);
            Classes.Set("nativeChrome", native);
        }
        else if (change.Property == IsActiveProperty)
        {
            Classes.Set("inactive", !change.GetNewValue<bool>());
        }
    }

    /// <summary>
    /// Moves the window. The second click of a double click is ignored so it can
    /// reach <see cref="ToggleMaximizedFromTitleBar"/> instead. It does nothing when
    /// the desktop draws the frame, since the desktop's own title bar carries the
    /// gesture and ours is then ordinary content.
    /// </summary>
    protected internal void BeginMoveWindow(PointerPressedEventArgs e)
    {
        if (UsesNativeChrome)
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
    /// The double click gesture on the title bar. It does nothing when the desktop
    /// draws the frame, since the desktop's own title bar already carries the gesture
    /// and ours is then ordinary content.
    /// </summary>
    protected internal void ToggleMaximizedFromTitleBar()
    {
        if (UsesNativeChrome)
        {
            return;
        }

        ToggleMaximized();
    }
}
