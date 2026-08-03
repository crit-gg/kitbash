using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A condition that is true right now, said in the layout rather than over it.
/// </summary>
public class Alert : TemplatedControl
{
    public static readonly StyledProperty<AlertTier> TierProperty =
        AvaloniaProperty.Register<Alert, AlertTier>(nameof(Tier));

    public static readonly StyledProperty<AlertForm> FormProperty =
        AvaloniaProperty.Register<Alert, AlertForm>(nameof(Form));

    /// <summary>
    /// The icon. The theme sets one per tier, so an alert always has one. A caller that
    /// names a glyph wins, since a local value outranks a control theme.
    /// </summary>
    public static readonly StyledProperty<IconGlyph> GlyphProperty =
        AvaloniaProperty.Register<Alert, IconGlyph>(nameof(Glyph));

    /// <summary>The line a person reads first. It wraps, so it can be a sentence.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Alert, string?>(nameof(Title));

    /// <summary>
    /// The detail under the title. The block form draws it, and the strip and the inline
    /// forms are one line, so neither of those does.
    /// </summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Alert, string?>(nameof(Description));

    /// <summary>
    /// What can be done about it. A row of buttons and links, and it always has a way to
    /// reach the full detail, because an alert is a summary of something longer.
    /// </summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<Alert, object?>(nameof(Actions));

    /// <summary>
    /// There is a way to close it. Only for an advisory condition: an alert saying
    /// something cannot be done has nothing to offer a person who closes it.
    /// </summary>
    public static readonly StyledProperty<bool> IsDismissableProperty =
        AvaloniaProperty.Register<Alert, bool>(nameof(IsDismissable));

    /// <summary>
    /// It is showing. Closing it turns this off, and a view model can bind it so the
    /// condition rather than the click decides. False takes the alert out of the layout
    /// entirely rather than leaving a gap where it was.
    /// </summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Alert, bool>(nameof(IsOpen), defaultValue: true);

    public Alert()
    {
        Close = new TemplateCommand(_ => IsOpen = false);
        Apply();
    }

    /// <inheritdoc cref="TierProperty"/>
    public AlertTier Tier
    {
        get => GetValue(TierProperty);
        set => SetValue(TierProperty, value);
    }

    /// <inheritdoc cref="FormProperty"/>
    public AlertForm Form
    {
        get => GetValue(FormProperty);
        set => SetValue(FormProperty, value);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public IconGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="ActionsProperty"/>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <inheritdoc cref="IsDismissableProperty"/>
    public bool IsDismissable
    {
        get => GetValue(IsDismissableProperty);
        set => SetValue(IsDismissableProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Closes it. What the dismiss button presses.</summary>
    public ICommand Close { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TierProperty || change.Property == FormProperty)
        {
            Apply();
        }
        else if (change.Property == IsOpenProperty)
        {
            IsVisible = change.GetNewValue<bool>();
        }
    }

    // The tier and the form become classes, so the theme selects on the alert rather
    // than reading an enum through a converter. Same shape as StatusPill.
    private void Apply()
    {
        foreach (var tier in Enum.GetValues<AlertTier>())
        {
            Classes.Set(ClassFor(tier), tier == Tier);
        }

        foreach (var form in Enum.GetValues<AlertForm>())
        {
            Classes.Set(ClassFor(form), form == Form);
        }
    }

    private static string ClassFor(AlertTier tier) => tier switch
    {
        AlertTier.Ok => "ok",
        AlertTier.Warn => "warn",
        AlertTier.Error => "error",
        _ => "info",
    };

    private static string ClassFor(AlertForm form) => form switch
    {
        AlertForm.Strip => "strip",
        AlertForm.Inline => "inline",
        _ => "block",
    };
}
