using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One row of a colour picker: a letter, a ramp and a spinbox over the same number. The
/// picker builds these and is the only thing that should.
/// </summary>
public partial class ColorChannel : ObservableObject
{
    [ObservableProperty]
    private double _value;

    /// <summary>Where the ramp's handle stands, 0 to 1. The ramp never carries the number
    /// itself, so a channel that goes above its ramp cannot be clamped by dragging it.</summary>
    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private IBrush? _ramp;

    internal ColorChannel(string label, double minimum, double maximum, double increment, string format)
    {
        Label = label;
        Minimum = minimum;
        Maximum = maximum;
        Increment = increment;
        Format = format;
    }

    /// <summary>The letter in front of the row, such as R or EV.</summary>
    public string Label { get; }

    /// <summary>The least the spinbox takes.</summary>
    public double Minimum { get; }

    /// <summary>The most the spinbox takes, which may be well past the end of the ramp.</summary>
    public double Maximum { get; }

    public double Increment { get; }

    /// <summary>What the spinbox writes the number with.</summary>
    public string Format { get; }

    /// <summary>The value at each end of the ramp.</summary>
    internal double RampMinimum { get; init; }

    internal double RampMaximum { get; init; } = 1;

    /// <summary>
    /// Where an edit goes. It is not called while the picker is the one writing, so a value
    /// pushed in never comes back as an edit.
    /// </summary>
    internal Action<double>? Edited { get; set; }

    /// <summary>True while the picker is pushing a value in.</summary>
    internal bool IsQuiet { get; set; }

    partial void OnValueChanged(double value)
    {
        if (!IsQuiet)
        {
            Edited?.Invoke(value);
        }
    }

    partial void OnPositionChanged(double value)
    {
        if (!IsQuiet)
        {
            Edited?.Invoke(RampMinimum + value * (RampMaximum - RampMinimum));
        }
    }
}
