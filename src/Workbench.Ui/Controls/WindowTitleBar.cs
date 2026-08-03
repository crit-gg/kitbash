using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// The title bar row a Workbench window uses. It supplies the icon, the title and the
/// caption buttons. Anything else a window wants in the chrome goes in its content.
/// </summary>
public class WindowTitleBar : ContentControl
{
    public static readonly StyledProperty<IImage?> IconProperty =
        AvaloniaProperty.Register<WindowTitleBar, IImage?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WindowTitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> VersionProperty =
        AvaloniaProperty.Register<WindowTitleBar, string?>(nameof(Version));

    private ChromelessWindow? _window;

    public WindowTitleBar()
    {
        DoubleTapped += OnDoubleTapped;
    }

    public IImage? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Sits after the title and reads quieter than it. Blank shows nothing. It is not
    /// part of <see cref="Title"/>, so the window's own title, and whatever the desktop
    /// and the task bar make of it, are left alone.
    /// </summary>
    public string? Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    /// <summary>
    /// Whether the window put anything of its own in the bar. An empty panel counts as
    /// nothing, so a window can leave a container in place and still collapse.
    /// </summary>
    public bool HasExtraContent => Content switch
    {
        null => false,
        string text => !string.IsNullOrWhiteSpace(text),
        Panel panel => panel.Children.Count > 0,
        _ => true,
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _window = this.FindAncestorOfType<ChromelessWindow>();

        // The window owns the choice, so follow it rather than caching a copy.
        if (_window is { } window)
        {
            window.PropertyChanged += OnWindowPropertyChanged;
        }

        Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_window is { } window)
        {
            window.PropertyChanged -= OnWindowPropertyChanged;
        }

        _window = null;
    }

    // The bar answers for the window it is in, so the theme never has to select across
    // the window and into this control's template.
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ChromelessWindow.UsesNativeChromeProperty
            || e.Property == Window.CanMinimizeProperty
            || e.Property == Window.WindowStateProperty
            || e.Property == Window.CanResizeProperty
            || e.Property == Window.IsActiveProperty)
        {
            Apply();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
        {
            Apply();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Wire(e, "PART_MinimizeButton", () => Minimize());
        Wire(e, "PART_MaximizeButton", () => _window?.ToggleMaximized());
        Wire(e, "PART_CloseButton", () => _window?.Close());
    }

    // A press on a caption button never reaches here, because Button marks the press
    // handled, so the bar cannot start a drag from one.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        _window?.BeginMoveWindow(e);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // A double tap bubbles, so one aimed at a control in the bar would otherwise
        // reach here as well and maximise the window behind it. Two quick clicks on a
        // button are two clicks, not a gesture on the bar.
        if (TakesItsOwnClicks(e.Source as Visual))
        {
            return;
        }

        _window?.ToggleMaximizedFromTitleBar();
        e.Handled = true;
    }

    /// <summary>
    /// Whether the tap landed on something that answers the pointer itself, anywhere
    /// between the source and this bar.
    /// </summary>
    private bool TakesItsOwnClicks(Visual? source)
    {
        for (var visual = source; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is InputElement { Focusable: true })
            {
                return true;
            }
        }

        return false;
    }

    private void Minimize()
    {
        if (_window is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void Wire(TemplateAppliedEventArgs e, string name, Action action)
    {
        if (e.NameScope.Find<Button>(name) is not { } button)
        {
            return;
        }

        button.Click += (_, _) => action();
    }

    /// <summary>
    /// Mirrors the window onto this control as classes, so the theme selects on the bar
    /// alone. Visibility is set here rather than styled, because it depends on the
    /// content and a selector cannot ask about that.
    /// </summary>
    private void Apply()
    {
        var native = _window?.UsesNativeChrome ?? false;

        Classes.Set("nativeChrome", native);
        Classes.Set("maximized", _window?.WindowState == WindowState.Maximized);
        Classes.Set("fixedSize", _window is { CanResize: false });
        Classes.Set("noMinimize", _window is { CanMinimize: false });

        // A bar with no window reads as active, which is what a preview should show.
        Classes.Set("inactive", _window is { IsActive: false });

        // SetCurrentValue rather than a local write, so a window that binds IsVisible
        // for its own reasons is not overridden for good.
        SetCurrentValue(IsVisibleProperty, !native || HasExtraContent);
    }
}
