using Avalonia;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// A status reads as colour plus icon plus label, never colour alone, so this control
/// carries all three and none of them is optional. A pill showing colour on its own is
/// a defect rather than a variant, which is why the icon has a default per tier instead
/// of a way to turn it off.
/// </summary>
public class StatusPill : TemplatedControl
{
    public static readonly StyledProperty<PillStatus> StatusProperty =
        AvaloniaProperty.Register<StatusPill, PillStatus>(nameof(Status));

    /// <summary>
    /// The icon. The theme sets one per tier, so a caller that says nothing still gets a
    /// pill that reads correctly, and a caller that names a glyph wins because a local
    /// value beats a control theme.
    /// </summary>
    public static readonly StyledProperty<IconGlyph> GlyphProperty =
        AvaloniaProperty.Register<StatusPill, IconGlyph>(nameof(Glyph));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    public StatusPill()
    {
        Apply(Status);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public PillStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public IconGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // The tier becomes a class, so the theme selects on the pill rather than reading an
    // enum through a converter. This is the same shape WindowTitleBar uses.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StatusProperty)
        {
            Apply(change.GetNewValue<PillStatus>());
        }
    }

    private void Apply(PillStatus status)
    {
        foreach (var tier in Enum.GetValues<PillStatus>())
        {
            Classes.Set(ClassFor(tier), tier == status);
        }
    }

    private static string ClassFor(PillStatus status) => status switch
    {
        PillStatus.Ok => "ok",
        PillStatus.Modified => "modified",
        PillStatus.Error => "error",
        PillStatus.Accent => "accent",
        _ => "neutral",
    };
}
