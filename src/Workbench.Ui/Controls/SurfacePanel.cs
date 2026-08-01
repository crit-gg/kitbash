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
/// <remarks>
/// A <see cref="HeaderedContentControl"/> is already a header over a body, so the footer
/// is the only thing added here. No built in type carries all three.
/// <para>
/// The header takes whatever it is given. A string is drawn as the panel title, and a
/// layout is drawn as it is written, which is how a title keeps actions or a note to its
/// right without this control naming a second slot.
/// </para>
/// <para>
/// A panel is one tone throughout. The header and the footer sit on the body's tone and
/// are separated from it by a hairline alone, so a fill change means a different panel
/// and nothing else. That is why <see cref="DialogFooter"/> is not reused here: it sits
/// on the root tone and aligns its content right, because a dialog's action row is
/// chrome rather than part of a panel.
/// </para>
/// <para>
/// The tone comes from <see cref="Surface"/> and is not written here. The theme sets
/// <c>Surface.Nests</c>, so a panel is one step below its parent wherever it is put.
/// </para>
/// </remarks>
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
