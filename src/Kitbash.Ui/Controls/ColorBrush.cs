using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Paints a <see cref="ColorValue"/>. A channel over 1 is clamped, since a swatch is a
/// screen colour whatever the value behind it holds.
/// </summary>
public class ColorBrush : IValueConverter
{
    public static readonly ColorBrush Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ColorValue color ? new SolidColorBrush(color.ToColor()) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A swatch is painted, never read.");
}
