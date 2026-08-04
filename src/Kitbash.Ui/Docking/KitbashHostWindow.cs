using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Dock.Avalonia.Controls;
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
///     [nameof(IDockWindow)] = () =&gt; new KitbashHostWindow(settings.UsesNativeChrome)
/// };
/// </code>
/// </example>
public class KitbashHostWindow : HostWindow
{
    /// <inheritdoc cref="ChromelessWindow.UsesNativeChromeProperty"/>
    public static readonly StyledProperty<bool> UsesNativeChromeProperty =
        AvaloniaProperty.Register<KitbashHostWindow, bool>(nameof(UsesNativeChrome));

    public KitbashHostWindow() => Wear(UsesNativeChrome);

    public KitbashHostWindow(bool usesNativeChrome)
        : this() =>
        UsesNativeChrome = usesNativeChrome;

    /// <inheritdoc cref="ChromelessWindow.UsesNativeChromeProperty"/>
    public bool UsesNativeChrome
    {
        get => GetValue(UsesNativeChromeProperty);
        set => SetValue(UsesNativeChromeProperty, value);
    }

    /// <summary>
    /// Dock keys its theme on its own type, so a subclass with a key of its own would find
    /// no theme and draw nothing.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(HostWindow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UsesNativeChromeProperty)
        {
            Wear(change.GetNewValue<bool>());
        }
    }

    /// <summary>
    /// The frame. Four of these are written as local values rather than left to the
    /// chromeless style, because Dock hands the desktop the frame from a style carrying an
    /// activator and a plain style will not beat one.
    /// </summary>
    private void Wear(bool native)
    {
        Classes.Set("chromeless", !native);
        Classes.Set("nativeChrome", native);

        WindowResize.SetGrips(this, !native);

        if (native)
        {
            // The desktop draws the whole frame, title bar included. Dock would hand it
            // the edge alone and keep the tool chrome as the title bar, which is our
            // chrome under another name.
            SetValue(WindowDecorationsProperty, WindowDecorations.Full);
            SetValue(ExtendClientAreaToDecorationsHintProperty, false);
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
