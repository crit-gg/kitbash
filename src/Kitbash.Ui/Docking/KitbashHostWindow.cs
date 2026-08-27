using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Dock.Avalonia.Controls;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Docking;

/// <summary>
/// The window a torn out dockable floats in. It wears the same frame every other Kitbash
/// window does and follows the same choice about who draws it, so a person who asked the
/// desktop for their frames gets one here too.
/// </summary>
/// <example>
/// <code>
/// HostWindowLocator = new Dictionary&lt;string, Func&lt;IHostWindow?&gt;&gt;
/// {
///     [nameof(IDockWindow)] = () =&gt; new KitbashHostWindow(settings.Chrome)
/// };
/// </code>
/// </example>
public class KitbashHostWindow : HostWindow
{
    /// <inheritdoc cref="ChromelessWindow.ChromeProperty"/>
    public static readonly StyledProperty<WindowChromeKind> ChromeProperty =
        AvaloniaProperty.Register<KitbashHostWindow, WindowChromeKind>(nameof(Chrome));

    private readonly WindowShadow _shadow;

    public KitbashHostWindow()
    {
        _shadow = new WindowShadow(this);

        Wear(Chrome);
    }

    public KitbashHostWindow(WindowChromeKind chrome, IWindowShadow? shadow = null)
        : this()
    {
        Chrome = chrome;
        Shadow = shadow;
    }

    /// <inheritdoc cref="ChromelessWindow.ChromeProperty"/>
    public WindowChromeKind Chrome
    {
        get => GetValue(ChromeProperty);
        set => SetValue(ChromeProperty, value);
    }

    /// <inheritdoc cref="ChromelessWindow.Shadow"/>
    public IWindowShadow? Shadow
    {
        get => _shadow.Service;
        set
        {
            _shadow.Service = value;
            _shadow.Declare();
        }
    }

    /// <summary>
    /// Dock keys its theme on its own type, so a subclass with a key of its own would find
    /// no theme and draw nothing.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(HostWindow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChromeProperty)
        {
            Wear(change.GetNewValue<WindowChromeKind>());
        }
        else if (change.Property == PaddingProperty)
        {
            _shadow.Declare();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _shadow.Declare();
    }

    /// <summary>
    /// The frame. Four of these are written as local values rather than left to the
    /// chromeless style, because Dock hands the desktop the frame from a style carrying an
    /// activator and a plain style will not beat one.
    /// </summary>
    private void Wear(WindowChromeKind chrome)
    {
        var drawn = chrome is WindowChromeKind.Drawn;

        Classes.Set("chromeless", drawn);
        Classes.Set("nativeChrome", chrome is WindowChromeKind.Desktop);
        Classes.Set("overlayChrome", chrome is WindowChromeKind.Overlay);

        WindowResize.SetGrips(this, drawn);

        if (!drawn)
        {
            // The desktop draws the whole frame, title bar included. Dock would hand it
            // the edge alone and keep the tool chrome as the title bar, which is our
            // chrome under another name. Overlay differs only in running the client area
            // under it, which is what puts its caption buttons on our row.
            SetValue(WindowDecorationsProperty, WindowDecorations.Full);
            SetValue(
                ExtendClientAreaToDecorationsHintProperty,
                chrome is WindowChromeKind.Overlay);
            ClearValue(BackgroundProperty);
            ClearValue(TransparencyLevelHintProperty);

            return;
        }

        SetValue(WindowDecorationsProperty, WindowDecorations.None);
        SetValue(ExtendClientAreaToDecorationsHintProperty, false);
        SetValue(BackgroundProperty, Brushes.Transparent);
        SetValue(TransparencyLevelHintProperty, new[] { WindowTransparencyLevel.Transparent });
    }
}
