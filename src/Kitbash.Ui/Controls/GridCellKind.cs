namespace Kitbash.Ui.Controls;

/// <summary>
/// What a column holds, which is what decides the affordance a cell offers before anything
/// is edited. A cell shows its value through the cell template and only becomes an editor on
/// a double click, so the chevron and the jump button have to be the cell's own.
/// </summary>
public enum GridCellKind
{
    /// <summary>No affordance of any kind. What a column is until it says otherwise.</summary>
    Plain,

    /// <summary>Free text. The only kind that underlines on row hover.</summary>
    Text,

    /// <summary>One of a list. A chevron on the trailing edge.</summary>
    Enum,

    /// <summary>A number. A stepper on the trailing edge, on cell hover alone.</summary>
    Number,

    /// <summary>A tick. The whole cell is the target.</summary>
    Boolean,

    /// <summary>Another record. A jump button on the trailing edge.</summary>
    Reference,

    /// <summary>A list of chips.</summary>
    Tags,

    /// <summary>A fraction, drawn as a bar behind the value.</summary>
    Ratio,

    /// <summary>A colour, drawn as a swatch beside its hex.</summary>
    Colour,
}
