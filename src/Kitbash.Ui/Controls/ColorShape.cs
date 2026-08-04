namespace Kitbash.Ui.Controls;

/// <summary>How a colour picker offers hue, saturation and value.</summary>
public enum ColorShape
{
    /// <summary>A saturation and value field with the hue on the bar beside it.</summary>
    Rectangle,

    /// <summary>Hue around the wheel and saturation out from its middle, value on the bar.</summary>
    Wheel,

    /// <summary>Neither. The channel rows are the whole of it.</summary>
    Sliders,
}
