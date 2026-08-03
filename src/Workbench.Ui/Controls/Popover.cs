using Avalonia;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// A floating card richer than a menu: a header, a body, and a footer of actions.
/// </summary>
public class Popover : HeaderedContentControl
{
    /// <summary>
    /// The row along the bottom. Actions in a menu popover, a readout in a picker.
    /// A popover with no footer does not draw one.
    /// </summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Popover, object?>(nameof(Footer));

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}
