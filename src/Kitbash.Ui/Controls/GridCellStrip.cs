namespace Kitbash.Ui.Controls;

/// <summary>
/// The cells of one row, built from the columns and kept in step with them. Both grids own
/// one, which is what stops a flat row and a tree grid row drifting apart.
/// </summary>
internal sealed class GridCellStrip(Func<GridColumn, int, DataGridCell> make)
{
    private readonly List<DataGridCell> cells = [];

    private GridCells? panel;
    private GridColumns? columns;

    public IReadOnlyList<DataGridCell> Cells => cells;

    /// <summary>The panel the cells are laid out in, found once the template applies.</summary>
    public void Attach(GridCells? next)
    {
        panel = next;
        Build();
    }

    /// <summary>
    /// The columns to draw, and whether that meant making the cells again. Nothing happens
    /// when they are the ones already held.
    /// </summary>
    public bool SetColumns(GridColumns? next)
    {
        if (ReferenceEquals(columns, next))
        {
            return false;
        }

        columns = next;
        Build();
        return true;
    }

    /// <summary>Makes the cells again, for when the set of columns itself has changed.</summary>
    public void Rebuild() => Build();

    /// <summary>Hands every cell the row's item, and closes any editor left open.</summary>
    public void Fill(object? item)
    {
        foreach (var cell in cells)
        {
            cell.IsEditing = false;
            cell.Content = item;
        }
    }

    /// <summary>Lays the cells out again after the column widths have moved.</summary>
    public void Relayout()
    {
        foreach (var cell in cells)
        {
            cell.IsVisible = cell.Column?.IsVisible ?? false;
        }

        panel?.InvalidateMeasure();
    }

    public DataGridCell? CellFor(GridColumn column) =>
        cells.FirstOrDefault(cell => ReferenceEquals(cell.Column, column));

    private void Build()
    {
        if (panel is null)
        {
            return;
        }

        panel.Children.Clear();
        cells.Clear();

        if (columns is null)
        {
            return;
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var cell = make(column, index);

            cell.Column = column;
            GridCells.SetColumn(cell, column);

            cells.Add(cell);
            panel.Children.Add(cell);
        }

        Relayout();
    }
}
