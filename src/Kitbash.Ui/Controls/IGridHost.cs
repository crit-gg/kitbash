namespace Kitbash.Ui.Controls;

/// <summary>
/// What a cell, a row or a header asks of the grid it is in. Both grids answer it, which is
/// what stops anything inside one naming a grid type and working in only one of them.
/// </summary>
internal interface IGridHost : IGridSorting
{
    /// <summary>The columns, shared with the header and every row.</summary>
    GridColumns Columns { get; }

    /// <summary>Where editing and the current cell are kept.</summary>
    GridBody Body { get; }
}
