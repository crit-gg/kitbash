namespace Kitbash.Ui.Controls;

/// <summary>
/// How a colour picker offers hue, saturation and value. The set is Godot's, and each member
/// is one of its own picker shapes.
/// </summary>
public enum ColorShape
{
    /// <summary>Saturation and value in a square, with hue on the bar beside it.</summary>
    Rectangle,

    /// <summary>Hue around a ring, with saturation and value in the square inside it.</summary>
    Wheel,

    /// <summary>Hue around the circle and saturation out from the middle, value on the bar.</summary>
    Circle,

    /// <summary>The same circle in the perceptual space, with lightness on the bar.</summary>
    OkhslCircle,

    /// <summary>Perceptual hue across and saturation up, with lightness on the bar.</summary>
    OkhsRectangle,

    /// <summary>Perceptual hue across and lightness up, with saturation on the bar.</summary>
    OkhlRectangle,

    /// <summary>None of them. The channel rows are the whole of it.</summary>
    Sliders,
}
