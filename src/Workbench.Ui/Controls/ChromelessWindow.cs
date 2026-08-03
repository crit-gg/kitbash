using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Workbench.Ui.Controls;

/// <summary>
/// A window that draws its own title bar. The frame lives in
/// Themes/Controls/WindowChrome.axaml, which supplies the resize grips wired up here.
/// </summary>
public class ChromelessWindow : Window
{
    /// <summary>
    /// Whether the desktop draws the frame. Set from
    /// <c>Workbench.Core.Settings.IWindowSettings</c> when the window is built. It is
    /// not watched, so changing the setting takes effect on the next launch.
    /// </summary>
    public static readonly StyledProperty<bool> UsesNativeChromeProperty =
        AvaloniaProperty.Register<ChromelessWindow, bool>(nameof(UsesNativeChrome));

    private static readonly string[] ResizeGripNames =
    [
        "PART_ResizeTopLeft",
        "PART_ResizeTop",
        "PART_ResizeTopRight",
        "PART_ResizeLeft",
        "PART_ResizeRight",
        "PART_ResizeBottomLeft",
        "PART_ResizeBottom",
        "PART_ResizeBottomRight",
    ];

    public ChromelessWindow()
    {
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        foreach (var name in ResizeGripNames)
        {
            if (e.NameScope.Find<Border>(name) is not { } grip)
            {
                continue;
            }

            grip.PointerPressed -= OnResizeGripPressed;
            grip.PointerPressed += OnResizeGripPressed;
        }
    }

    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize || sender is not Border { Tag: WindowEdge edge })
        {
            return;
        }

        BeginResizeDrag(edge, e);
        e.Handled = true;
    }
}
