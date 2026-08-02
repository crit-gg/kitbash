using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// A row of options with one of them chosen, and a thumb that slides to whichever it is.
/// The options are radios, so grouping, clicking and the keyboard are the framework's.
/// </summary>
/// <remarks>
/// A radio in a group is what a segmented control already is, so the only thing this type
/// adds is the thumb. It is here because the thumb has to outlive the choice: a fill on
/// the chosen option would appear and disappear, and a thing that slides has to be one
/// thing that moves.
/// <para>
/// The thumb reads the chosen option rather than working its place out from tokens, so
/// the padding, the gap between options and the row height are written once in the theme
/// and never restated here. Options are different widths, so both the position and the
/// width move.
/// </para>
/// <para>
/// The motion belongs to the theme, through <see cref="ThumbTransitions"/>. That is the
/// shape <c>ToggleSwitch.KnobTransitions</c> already has, and for the same reason: a
/// duration and a curve are look rather than behaviour.
/// </para>
/// <para>
/// Only a change of answer slides. The first placement, a resize and coming back into a
/// tree are all written with the transitions off, so a row is drawn correct rather than
/// sliding in from its left edge, and a thumb never trails the row it belongs to while a
/// window is being dragged.
/// </para>
/// </remarks>
[TemplatePart(ThumbPart, typeof(Border))]
[TemplatePart(TravelPart, typeof(Canvas))]
public class Segmented : ItemsControl
{
    /// <summary>
    /// How the thumb moves. The theme writes it and the control hands it to the thumb
    /// once the thumb is standing in the right place.
    /// </summary>
    public static readonly StyledProperty<Transitions?> ThumbTransitionsProperty =
        AvaloniaProperty.Register<Segmented, Transitions?>(nameof(ThumbTransitions));

    private const string ThumbPart = "PART_Thumb";
    private const string TravelPart = "PART_Travel";

    private Border? _thumb;
    private Canvas? _travel;
    private RadioButton? _chosen;

    /// <summary>The thumb is standing somewhere real, so the next move may be a slide.</summary>
    private bool _placed;

    public Segmented()
    {
        // One place an option is followed and one place it is dropped, since a container
        // that is told something has to be untold in the same breath.
        ContainerPrepared += (_, e) => Follow(e.Container);
        ContainerClearing += (_, e) => Unfollow(e.Container);
    }

    /// <inheritdoc cref="ThumbTransitionsProperty"/>
    public Transitions? ThumbTransitions
    {
        get => GetValue(ThumbTransitionsProperty);
        set => SetValue(ThumbTransitionsProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _thumb = e.NameScope.Find<Border>(ThumbPart);
        _travel = e.NameScope.Find<Canvas>(TravelPart);
        _placed = false;

        if (_thumb is not null)
        {
            _thumb.Transitions = null;
            _thumb.IsVisible = false;
        }

        Choose(sliding: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Coming back is a first load again. The row may be laid out somewhere else at
        // another width, and a slide from where it used to be would be a lie.
        _placed = false;

        if (_thumb is not null)
        {
            _thumb.Transitions = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ThumbTransitionsProperty && _placed && _thumb is not null)
        {
            _thumb.Transitions = ThumbTransitions;
        }
    }

    private void Follow(Control container)
    {
        if (container is RadioButton option)
        {
            option.PropertyChanged += OnOptionChanged;
        }

        // An option can arrive already checked, which is a row that opens with an answer.
        Choose(sliding: false);
    }

    private void Unfollow(Control container)
    {
        if (container is RadioButton option)
        {
            option.PropertyChanged -= OnOptionChanged;
        }

        Choose(sliding: false);
    }

    /// <remarks>
    /// Bounds are read from here rather than from an observable, because an observable
    /// answers with the current value the moment it is subscribed to, which would snap
    /// the thumb onto the option that was just picked and take the slide away.
    /// </remarks>
    private void OnOptionChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ToggleButton.IsCheckedProperty)
        {
            Choose(sliding: true);
        }
        else if (change.Property == BoundsProperty && ReferenceEquals(sender, _chosen))
        {
            Place(sliding: false);
        }
    }

    /// <summary>
    /// Reads the row rather than following the change that arrived. Picking an option
    /// unchecks its sibling, so two changes are raised and the order between them is not
    /// ours, while the answer is the same whichever one is being handled.
    /// </summary>
    private void Choose(bool sliding)
    {
        RadioButton? chosen = null;

        foreach (var container in GetRealizedContainers())
        {
            if (container is RadioButton option && option.IsChecked == true)
            {
                chosen = option;
                break;
            }
        }

        _chosen = chosen;
        Place(sliding);
    }

    private void Place(bool sliding)
    {
        if (_thumb is null || _travel is null)
        {
            return;
        }

        // Nothing chosen means no thumb, so a row with no answer does not claim one.
        // Zero bounds means the row has not been laid out yet, and placing on that would
        // be the slide from the left edge this exists to prevent.
        if (_chosen is not { Bounds.Width: > 0, Bounds.Height: > 0 } option)
        {
            _thumb.IsVisible = false;
            return;
        }

        if (option.TranslatePoint(default, _travel) is not { } at)
        {
            return;
        }

        var slides = sliding && _placed;

        if (!slides)
        {
            _thumb.Transitions = null;
        }

        Canvas.SetLeft(_thumb, at.X);
        Canvas.SetTop(_thumb, at.Y);
        _thumb.Width = option.Bounds.Width;
        _thumb.Height = option.Bounds.Height;
        _thumb.IsVisible = true;

        _placed = true;

        if (!slides)
        {
            _thumb.Transitions = ThumbTransitions;
        }
    }
}
