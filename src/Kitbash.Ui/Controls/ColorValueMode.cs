namespace Kitbash.Ui.Controls;

/// <summary>Which numbers a colour picker's channel rows read.</summary>
public enum ColorValueMode
{
    /// <summary>Red, green, blue and alpha, each 0 to 255.</summary>
    Rgb,

    /// <summary>Hue in degrees, saturation and value as percentages, alpha 0 to 255.</summary>
    Hsv,

    /// <summary>The four floats themselves, which is the only mode that shows a channel over 1.</summary>
    Raw,

    /// <summary>Hue in degrees, saturation and lightness as percentages, alpha as a percentage.</summary>
    Okhsl,
}
