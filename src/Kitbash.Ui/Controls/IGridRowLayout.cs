namespace Kitbash.Ui.Controls;

/// <summary>
/// A row that lays its content out in columns. Both grids have one and neither shares a
/// base type with the other, so this is what a grid tells its realised rows through.
/// </summary>
internal interface IGridRowLayout
{
    /// <summary>The cells this row is drawing, one per column, in the order they sit in.</summary>
    IReadOnlyList<DataGridCell> Cells { get; }

    /// <summary>The column widths have moved.</summary>
    void Relayout();

    /// <summary>The set of columns itself has changed, so the cells have to be made again.</summary>
    void Rebuild();

    /// <summary>A cell in this row is open for editing. The grid is what decides.</summary>
    void SetEditing(bool editing);

    /// <summary>
    /// A cell in this row is where the keyboard is, so the row gives up its focus line. A
    /// row wearing a fill, an outline and a cell border at once cannot be read.
    /// </summary>
    void SetCurrent(bool current);
}
