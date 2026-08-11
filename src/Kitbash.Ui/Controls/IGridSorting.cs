namespace Kitbash.Ui.Controls;

/// <summary>
/// What a header cell asks when its column is clicked. Both grids answer it, which is what
/// stops a header naming one grid and doing nothing in the other.
/// </summary>
internal interface IGridSorting
{
    /// <summary>Cycles the column up, then down, then back to the order it came in.</summary>
    void SortBy(GridColumn column);
}

/// <summary>
/// Orders two items by a column's key. It is what turns a column into something a row
/// list can sort by, in either direction.
/// </summary>
internal sealed class GridKeyComparer(
    Func<object, object?> key,
    IComparer<object?> values,
    bool descending) : IComparer<object>
{
    public int Compare(object? left, object? right)
    {
        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        if (right is null)
        {
            return 1;
        }

        var order = values.Compare(key(left), key(right));

        return descending ? -order : order;
    }
}
