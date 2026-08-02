using Avalonia;
using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>
/// A short label in a pill, naming what something is. A runtime, a format, a workspace's
/// access.
/// </summary>
/// <remarks>
/// This is not a <see cref="StatusPill"/> and must not become one. A status reads as
/// colour plus icon plus label and the pill enforces all three, because a status shown in
/// colour alone is unreadable to a good share of people. A badge is a word, and the
/// colour behind it is decoration rather than meaning, so it carries no icon and its dot
/// is optional.
/// <para>
/// The test for which to reach for: if the colour is carrying information that the text
/// does not already say, it is a status and belongs in a pill.
/// </para>
/// </remarks>
public class Badge : ContentControl
{
    public static readonly StyledProperty<BadgeTier> TierProperty =
        AvaloniaProperty.Register<Badge, BadgeTier>(nameof(Tier));

    /// <summary>
    /// Whether a round mark sits before the label. It takes the tier's own colour, and it
    /// is round rather than square, which is what tells it apart from a chip's mark.
    /// </summary>
    public static readonly StyledProperty<bool> HasDotProperty =
        AvaloniaProperty.Register<Badge, bool>(nameof(HasDot));

    public Badge()
    {
        Apply(Tier);
    }

    /// <inheritdoc cref="TierProperty"/>
    public BadgeTier Tier
    {
        get => GetValue(TierProperty);
        set => SetValue(TierProperty, value);
    }

    /// <inheritdoc cref="HasDotProperty"/>
    public bool HasDot
    {
        get => GetValue(HasDotProperty);
        set => SetValue(HasDotProperty, value);
    }

    // The tier becomes a class, so the theme selects on the badge rather than reading an
    // enum through a converter. The same shape StatusPill and WindowTitleBar use.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TierProperty)
        {
            Apply(change.GetNewValue<BadgeTier>());
        }
    }

    private void Apply(BadgeTier tier)
    {
        foreach (var one in Enum.GetValues<BadgeTier>())
        {
            Classes.Set(ClassFor(one), one == tier);
        }
    }

    private static string ClassFor(BadgeTier tier) => tier switch
    {
        BadgeTier.Ok => "ok",
        BadgeTier.Modified => "modified",
        BadgeTier.Error => "error",
        BadgeTier.Accent => "accent",
        BadgeTier.Data => "data",
        BadgeTier.Graph => "graph",
        _ => "neutral",
    };
}
