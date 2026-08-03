using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace Workbench.Ui.Controls;

/// <summary>
/// A header over a body, with a footer under it, on a tone one step below whatever it
/// sits on. The surface a tool is assembled from.
/// </summary>
[TemplatePart("PART_Footer", typeof(ContentPresenter))]
public class SurfacePanel : HeaderedContentControl
{
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SurfacePanel, object?>(nameof(Footer));

    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<SurfacePanel, IDataTemplate?>(nameof(FooterTemplate));

    /// <summary>
    /// The row under the body. Nothing is drawn when there is none, so a panel without
    /// one is a header over a body and no empty strip.
    /// </summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public IDataTemplate? FooterTemplate
    {
        get => GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }
}
