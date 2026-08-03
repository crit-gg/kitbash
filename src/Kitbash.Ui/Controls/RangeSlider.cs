using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A slider with two knobs and the fill between them.
/// </summary>
[TemplatePart(TrackPart, typeof(Canvas))]
[TemplatePart(FillPart, typeof(Control))]
[TemplatePart(LowerPart, typeof(Thumb))]
[TemplatePart(UpperPart, typeof(Thumb))]
public class RangeSlider : TemplatedControl
{
    private const string TrackPart = "PART_Track";
    private const string FillPart = "PART_Fill";
    private const string LowerPart = "PART_LowerThumb";
    private const string UpperPart = "PART_UpperThumb";

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Maximum), 1d);

    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(
            nameof(LowerValue), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(
            nameof(UpperValue), 1d, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private Canvas? _track;
    private Control? _fill;
    private Thumb? _lower;
    private Thumb? _upper;

    static RangeSlider()
    {
        AffectsArrange<RangeSlider>(LowerValueProperty, UpperValueProperty, MinimumProperty, MaximumProperty);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The near end of the range. Never above <see cref="UpperValue"/>.</summary>
    public double LowerValue
    {
        get => GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    /// <summary>The far end of the range. Never below <see cref="LowerValue"/>.</summary>
    public double UpperValue
    {
        get => GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Release(_lower);
        Release(_upper);

        _track = e.NameScope.Find<Canvas>(TrackPart);
        _fill = e.NameScope.Find<Control>(FillPart);
        _lower = e.NameScope.Find<Thumb>(LowerPart);
        _upper = e.NameScope.Find<Thumb>(UpperPart);

        Hold(_lower);
        Hold(_upper);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        Place();
        return size;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Neither end may pass the other, and the one that just moved is the one that
        // stays where it was put.
        if (change.Property == LowerValueProperty && LowerValue > UpperValue)
        {
            UpperValue = LowerValue;
        }
        else if (change.Property == UpperValueProperty && UpperValue < LowerValue)
        {
            LowerValue = UpperValue;
        }
    }

    private void Hold(Thumb? thumb)
    {
        if (thumb is not null)
        {
            thumb.DragDelta += OnDragDelta;
        }
    }

    private void Release(Thumb? thumb)
    {
        if (thumb is not null)
        {
            thumb.DragDelta -= OnDragDelta;
        }
    }

    private void OnDragDelta(object? sender, VectorEventArgs e)
    {
        if (_track is null || Travel() <= 0)
        {
            return;
        }

        var moved = e.Vector.X / Travel() * Span();

        if (ReferenceEquals(sender, _lower))
        {
            LowerValue = Clamp(LowerValue + moved, Minimum, UpperValue);
        }
        else
        {
            UpperValue = Clamp(UpperValue + moved, LowerValue, Maximum);
        }
    }

    private void Place()
    {
        if (_track is null || _lower is null || _upper is null)
        {
            return;
        }

        var lower = At(LowerValue);
        var upper = At(UpperValue);

        Canvas.SetLeft(_lower, lower);
        Canvas.SetLeft(_upper, upper);

        if (_fill is not null)
        {
            // From the middle of one knob to the middle of the other, so the fill meets
            // both rather than starting at an edge.
            var half = _lower.Bounds.Width / 2;
            Canvas.SetLeft(_fill, lower + half);
            _fill.Width = Math.Max(0, upper - lower);
        }
    }

    private double At(double value) =>
        Span() <= 0 ? 0 : (value - Minimum) / Span() * Travel();

    private double Span() => Maximum - Minimum;

    private double Travel() =>
        _track is null || _lower is null ? 0 : Math.Max(0, _track.Bounds.Width - _lower.Bounds.Width);

    private static double Clamp(double value, double low, double high) =>
        value < low ? low : value > high ? high : value;
}
