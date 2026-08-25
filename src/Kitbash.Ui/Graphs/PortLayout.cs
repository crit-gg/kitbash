namespace Kitbash.Ui.Controls;

/// <summary>
/// What a pin is drawn as. Colour alone says a type, and a shape says it again for anyone who
/// cannot tell two colours apart.
/// </summary>
public enum PortShape
{
    Round,

    /// <summary>A square on its corner. What a material graph draws a vector on.</summary>
    Diamond,

    Square,
}

/// <summary>How a node spaces its pins.</summary>
public enum PortLayout
{
    /// <summary>A row per port under the header, with its name beside it.</summary>
    Rows,

    /// <summary>Spread evenly down the node's outer edge, with the name shown on hover.</summary>
    Edge,
}
