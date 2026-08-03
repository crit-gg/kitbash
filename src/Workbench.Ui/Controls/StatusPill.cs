using Avalonia;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// A status as colour, icon and label together. None of the three is optional, and each
/// tier carries a default glyph, so a pill cannot say its meaning in colour alone.
/// </summary>
public class StatusPill : TemplatedControl
{
    public static readonly StyledProperty<PillStatus> StatusProperty =
        AvaloniaProperty.Register<StatusPill, PillStatus>(nameof(Status));

    /// <summary>
    /// The icon. The theme sets one per tier, so a pill always has one. A caller that
    /// names a glyph wins, since a local value outranks a control theme.
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
