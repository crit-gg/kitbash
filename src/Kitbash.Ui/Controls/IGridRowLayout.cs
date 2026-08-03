namespace Kitbash.Ui.Controls;

/// <summary>
/// A row that lays its content out in columns. Both grids have one and neither shares a
/// base type with the other, so this is what a grid tells its realised rows through.
/// </summary>
internal interface IGridRowLayout
{
    /// <summary>The column widths have moved.</summary>
    void Relayout();

    /// <summary>The set of columns itself has changed, so the cells have to be made again.</summary>
    void Rebuild();
}
