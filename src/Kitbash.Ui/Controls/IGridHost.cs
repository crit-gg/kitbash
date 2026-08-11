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

    /// <summary>Whether this grid groups rows at all, which a tree does not.</summary>
    bool CanGroup { get; }

    /// <summary>Groups by a column, or drops the grouping when it is already on that one.</summary>
    void GroupBy(GridColumn column);
}
