using Avalonia.Automation.Peers;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

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

        if (row?.Item is INotifyPropertyChanged before)
        {
            before.PropertyChanged -= OnItemChanged;
        }

        if (row?.Item is INotifyDataErrorInfo reported)
        {
            reported.ErrorsChanged -= OnItemErrored;
        }

        row = next;

        if (row is not null)
        {
            row.PropertyChanged += OnRowChanged;
        }

        // The row's own marks are read off the item, so a value changing has to reach the
        // row rather than waiting for the next time the grid redraws.
        if (row?.Item is INotifyPropertyChanged after)
        {
            after.PropertyChanged += OnItemChanged;
        }

        if (row?.Item is INotifyDataErrorInfo reporting)
        {
            reporting.ErrorsChanged += OnItemErrored;
        }

        PseudoClasses.Set(":alt", row?.IsAlternate == true);
        strip.Fill(row?.Item);
    }

    /// <summary>The cell drawing a column, or null when the row has no such cell.</summary>
    internal DataGridCell? CellFor(GridColumn column) => strip.CellFor(column);

    IReadOnlyList<DataGridCell> IGridRowLayout.Cells => strip.Cells;

    void IGridRowLayout.Relayout() => strip.Relayout();

    void IGridRowLayout.Rebuild() => strip.Rebuild();

    void IGridRowLayout.SetEditing(bool editing) => PseudoClasses.Set(":editing", editing);

    void IGridRowLayout.SetCurrent(bool current) => PseudoClasses.Set(":cell", current);

    void IGridRowLayout.SetPinOffset(double offset) => strip.SetPinOffset(offset);

    void IGridRowLayout.SetModified(bool modified) => PseudoClasses.Set(":modified", modified);

    object? IGridRowLayout.Held => row?.Item;

    protected override AutomationPeer OnCreateAutomationPeer() => new DataGridRowAutomationPeer(this);

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

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (this.FindAncestorOfType<IGridHost>()?.Body is { Marks: true } body)
        {
            body.Mark(this);
        }
    }

    private void OnItemErrored(object? sender, DataErrorsChangedEventArgs e)
    {
        if (this.FindAncestorOfType<IGridHost>()?.Body is { } body)
        {
            body.Mark(this);
            body.Recount();
        }
    }
}
