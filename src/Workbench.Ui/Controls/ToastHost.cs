using Avalonia;
using Avalonia.Controls.Primitives;
using Workbench.Ui.Toasts;

namespace Workbench.Ui.Controls;

/// <summary>
/// Where one toast service draws. Eight regions laid over whatever this is placed in.
/// </summary>
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
