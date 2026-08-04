namespace Kitbash.Ui.Controls;

/// <summary>Which numbers a colour picker's channel rows read.</summary>
public enum ColorValueMode
{
    /// <summary>Red, green, blue and alpha, each 0 to 255.</summary>
    Rgb,

    /// <summary>Hue in degrees, saturation and value as percentages, alpha 0 to 255.</summary>
    Hsv,

    /// <summary>
    /// The colour as light, which is what Godot's Linear mode reads. It is the sRGB
    /// transfer taken off, so 0.878 in the numbers a colour is written with is 0.744 here.
    /// </summary>
    Linear,

    /// <summary>Hue in degrees, saturation and lightness as percentages, alpha 0 to 255.</summary>
    Okhsl,
}
