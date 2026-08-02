using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Workbench.Ui.Controls;

/// <summary>
/// Grows a corner radius by the reach of the focus halo, so the halo stays concentric
/// with the control it hugs.
/// </summary>
/// <remarks>
/// The halo sits outside the border on a negative margin, and a corner radius describes
/// the outer edge, so the halo's radius is the control's plus the halo's thickness.
/// Naming a token per radius works only until a control takes a radius nobody thought
/// about: the search field is 8 rather than 5, and it wore the 5px control's ring.
/// <para>
/// A corner that is square stays square. That is what keeps one end of a split control
/// right, where the radius is 5,0,0,5 and the halo has to be 7,0,0,7 rather than 7 all
/// round.
/// </para>
/// </remarks>
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
