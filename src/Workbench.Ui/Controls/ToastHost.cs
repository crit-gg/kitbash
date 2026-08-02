using Avalonia;
using Avalonia.Controls.Primitives;
using Workbench.Ui.Toasts;

namespace Workbench.Ui.Controls;

/// <summary>
/// Where one toast service draws. Eight regions laid over whatever this is placed in.
/// </summary>
/// <remarks>
/// A window puts one of these over its content and gives it the application's service.
/// A tool panel that reports inside itself puts another over its own content and gives
/// it a service of its own, from <see cref="IToastServiceFactory"/>. It clips, so a
/// panel's toasts stay in the panel.
/// <para>
/// It draws nothing itself and has no fill, so everything underneath is still reachable
/// wherever a card is not.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Panel&gt;
///     &lt;views:ThePage /&gt;
///     &lt;ui:ToastHost Service="{Binding Toasts}" /&gt;
/// &lt;/Panel&gt;
/// </code>
/// </example>
public class ToastHost : TemplatedControl
{
    public static readonly StyledProperty<IToastService?> ServiceProperty =
        AvaloniaProperty.Register<ToastHost, IToastService?>(nameof(Service));

    /// <inheritdoc cref="ServiceProperty"/>
    public IToastService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }
}
