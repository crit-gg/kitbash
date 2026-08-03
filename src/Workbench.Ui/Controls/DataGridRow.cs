using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// One row of data. A list row that lays its content out in columns rather than across the
/// whole width, so the seven row states from the list apply to it unchanged.
/// </summary>
public class DataGridRow : ListBoxItem, IGridRowLayout
{
    private const string CellsPart = "PART_Cells";

    private readonly GridCellStrip strip;

    private GridRow? row;

    public DataGridRow()
    {
        strip = new GridCellStrip(CreateCell);
    }

    /// <summary>The row this container is standing in for, or null while it is spare.</summary>
    public GridRow? Row => row;

    /// <summary>Where the columns come from. The grid hands this over when it prepares the row.</summary>
    internal void Attach(GridColumns? columns) => strip.SetColumns(columns);

    /// <summary>
    /// Takes on a row, or gives up the one it had. Null releases the container, which is
    /// what stops a recycled row carrying the last one's content.
    /// </summary>
    internal void Follow(GridRow? next)
    {
        if (row is not null)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        row = next;

        if (row is not null)
        {
            row.PropertyChanged += OnRowChanged;
        }

        PseudoClasses.Set(":alt", row?.IsAlternate == true);
        strip.Fill(row?.Item);
    }

    /// <summary>The cell drawing a column, or null when the row has no such cell.</summary>
    internal DataGridCell? CellFor(GridColumn column) => strip.CellFor(column);

    /// <summary>Says a cell in this row is open for editing. The grid is what decides.</summary>
    internal void SetEditing(bool editing) => PseudoClasses.Set(":editing", editing);

    void IGridRowLayout.Relayout() => strip.Relayout();

    void IGridRowLayout.Rebuild() => strip.Rebuild();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        strip.Attach(e.NameScope.Find<GridCells>(CellsPart));
        strip.Fill(row?.Item);
    }

    /// <summary>What goes in a cell. The tree grid overrides it to lead with its caret.</summary>
    protected virtual DataGridCell CreateCell(GridColumn column, int index) => new();

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GridRow changed && ReferenceEquals(changed, row))
        {
            PseudoClasses.Set(":alt", changed.IsAlternate);
        }
    }
}
