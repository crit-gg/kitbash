using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Workbench.Ui.Controls;

/// <summary>
/// One row of a tree grid. A tree row that lays its content out in columns, with the
/// hierarchy in the one column that leads.
/// </summary>
public class TreeDataGridRow : TreeItem, IGridRowLayout
{
    private const string CellsPart = "PART_Cells";

    private readonly GridCellStrip strip;

    private GridColumn? lead;

    public TreeDataGridRow()
    {
        strip = new GridCellStrip(CreateCell);
    }

    /// <summary>Where the columns come from, and which of them carries the hierarchy.</summary>
    internal void Attach(GridColumns? columns, GridColumn? leading)
    {
        var moved = !ReferenceEquals(lead, leading);

        lead = leading;

        if (!strip.SetColumns(columns) && moved)
        {
            strip.Rebuild();
        }
    }

    internal override void Follow(TreeRow? next)
    {
        base.Follow(next);

        strip.Fill(next?.Item);
        UpdateLead();
    }

    void IGridRowLayout.Relayout() => strip.Relayout();

    void IGridRowLayout.Rebuild() => strip.Rebuild();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        strip.Attach(e.NameScope.Find<GridCells>(CellsPart));
        strip.Fill(Row?.Item);
        UpdateLead();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LevelProperty
            || change.Property == HasChildrenProperty
            || change.Property == IsExpandedProperty
            || change.Property == IndentSizeProperty)
        {
            UpdateLead();
        }
    }

    /// <summary>
    /// Which cell leads. The first column when nothing has named one, since the design
    /// puts a picker in front of the hierarchy and only the caller knows that.
    /// </summary>
    private DataGridCell CreateCell(GridColumn column, int index)
    {
        var leading = lead is null ? index == 0 : ReferenceEquals(column, lead);

        return leading ? new TreeDataGridCell() : new DataGridCell();
    }

    private void UpdateLead()
    {
        foreach (var cell in strip.Cells.OfType<TreeDataGridCell>())
        {
            cell.Level = Level;
            cell.HasChildren = HasChildren;
            cell.IsExpanded = IsExpanded;
        }
    }
}
