using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Kitbash.Ui.Controls;

/// <summary>
/// How wide the wash behind a bounded number is. It takes the value, the two ends and the
/// well's own bounds, in that order, and answers a width in pixels.
/// </summary>
public sealed class RangeFill : IMultiValueConverter
{
    public static RangeFill Instance { get; } = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 4
            || Number(values[0]) is not { } value
            || Number(values[1]) is not { } low
            || Number(values[2]) is not { } high
            || values[3] is not Rect bounds)
        {
            return 0d;
        }

        // Two ends that meet describe no range, so nothing is drawn rather than all of it.
        if (high <= low)
        {
            return 0d;
        }

        return Math.Clamp((value - low) / (high - low), 0, 1) * bounds.Width;
    }

    private static double? Number(object? value) => value switch
    {
        double number => number,
        decimal number => (double)number,
        int number => number,
        _ => null,
    };
}
