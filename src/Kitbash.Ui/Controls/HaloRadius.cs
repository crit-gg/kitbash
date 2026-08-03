using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Grows a corner radius by the reach of the focus halo, so the halo stays concentric
/// with the control it hugs.
/// </summary>
public class HaloRadius : IValueConverter
{
    /// <summary>The halo is 2px, which is <c>FocusHaloThickness</c>.</summary>
    private const double Reach = 2;

    public static readonly IValueConverter Grown = new HaloRadius();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CornerRadius radius
            ? new CornerRadius(
                Grow(radius.TopLeft),
                Grow(radius.TopRight),
                Grow(radius.BottomRight),
                Grow(radius.BottomLeft))
            : AvaloniaProperty.UnsetValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        AvaloniaProperty.UnsetValue;

    private static double Grow(double corner) => corner > 0 ? corner + Reach : 0;
}
