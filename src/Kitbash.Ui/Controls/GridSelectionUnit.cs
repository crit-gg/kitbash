namespace Kitbash.Ui.Controls;

/// <summary>
/// What a person picks in a grid. It changes what a click and a shift click already mean,
/// so it is a mode rather than something a grid does both of.
/// </summary>
public enum GridSelectionUnit
{
    /// <summary>
    /// Whole rows, which is what every grid in the launcher wants. Shift and a drag extend
    /// the row selection and there is no such thing as a range of cells.
    /// </summary>
    Row,

    /// <summary>
    /// A block of cells. Shift extends from the anchor to make a rectangle, and copy, the
    /// footer's counts and anything that writes all work over that block.
    /// </summary>
    Cell,
}
