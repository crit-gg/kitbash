namespace Kitbash.Ui.Controls;

/// <summary>One column of a sort and which way it runs.</summary>
/// <param name="Column">The column being sorted on. It always has a sort key.</param>
/// <param name="Direction">Which way it runs, never <see cref="GridSortDirection.None"/>.</param>
public readonly record struct GridSortTerm(GridColumn Column, GridSortDirection Direction);

/// <summary>
/// Orders two items by a run of columns, the first one that separates them deciding. Each
/// column brings its own comparer, or the built in one when it has none.
/// </summary>
internal sealed class GridSortComparer(IReadOnlyList<GridSortTerm> terms, IComparer<object?> values)
    : IComparer<object>
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

        foreach (var term in terms)
        {
            if (term.Column.SortKey is not { } key)
            {
                continue;
            }

            var order = (term.Column.Comparer ?? values).Compare(key(left), key(right));

            if (order != 0)
            {
                return term.Direction == GridSortDirection.Descending ? -order : order;
            }
        }

        return 0;
    }
}
