namespace Kitbash.Ui.Controls;

/// <summary>
/// What a person may do to the values in a grid. Everything that writes is off by default,
/// because all of it writes to more than one cell at a time and all of it arrives through
/// keys a person hits while meaning something else.
/// </summary>
[Flags]
public enum CellActions
{
    None = 0,

    /// <summary>Control and C. On by default, since reading is not writing.</summary>
    Copy = 1,

    /// <summary>Control and X.</summary>
    Cut = 2,

    /// <summary>Control and V.</summary>
    Paste = 4,

    /// <summary>Delete, which empties what is picked.</summary>
    Clear = 8,

    /// <summary>Dragging the handle at the corner of a range over the cells beside it.</summary>
    Fill = 16,
}
