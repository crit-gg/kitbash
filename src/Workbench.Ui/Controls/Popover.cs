using Avalonia;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// A floating card richer than a menu: a header, a body, and a footer of actions.
/// </summary>
/// <remarks>
/// It is a <see cref="HeaderedContentControl"/> with a footer added, because a header
/// over content is exactly what that type already is and only the footer is missing.
/// <para>
/// One body, two hosts. Floating it takes the overlay surface and a shadow. Given the
/// <c>inPanel</c> class it drops the shadow and takes the surface it was dropped onto,
/// which is what lets a picker open in a popover and also sit inside a property panel
/// without being built twice.
/// </para>
/// <para>
/// The radius matches the control that opened it rather than being larger, so a popover
/// reads as an extension of its trigger rather than a separate floating card.
/// </para>
/// </remarks>
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
