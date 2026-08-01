using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Workbench.Views;

/// <summary>
/// A window that draws its own title bar. The frame lives in
/// Themes/WindowChrome.axaml, which supplies the resize grips wired up here.
/// </summary>
/// <remarks>
/// The view also tags its title bar and caption buttons with
/// WindowDecorationProperties.ElementRole, which is the platform hit testing route
/// added in Avalonia 12. Those roles only take effect when
/// ExtendClientAreaToDecorationsHint is honored, which on Linux needs an experimental
/// option that is off by default, so moving and resizing are driven from here for
/// now. See .claude/avalonia.md.
/// </remarks>
public class ChromelessWindow : Window
{
    /// <summary>
    /// Whether the desktop draws the frame. Set from
    /// <see cref="Workbench.Core.Settings.IWindowSettings"/> when the window is built.
    /// It is not watched, so changing the setting takes effect on the next launch.
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

        // The desktop shows this in the task bar and the window list. The largest
        // size is used so the desktop scales down rather than up.
        using var icon = AssetLoader.Open(
            new Uri("avares://Workbench/Assets/Icons/icon_256x256.png"));

        Icon = new WindowIcon(icon);
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
    /// The two classes pick the frame. <c>chromeless</c> selects the drawn template and
    /// its resize grips, <c>nativeChrome</c> drops the caption buttons because the
    /// desktop supplies its own. They are mutually exclusive.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != UsesNativeChromeProperty)
        {
            return;
        }

        var native = change.GetNewValue<bool>();

        Classes.Set("chromeless", !native);
        Classes.Set("nativeChrome", native);
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
