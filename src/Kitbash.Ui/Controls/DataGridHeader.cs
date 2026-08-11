using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The row of column titles. It scrolls sideways with the body and never up and down, so
/// it stays over the columns it names however far across the grid has been taken.
/// </summary>
public class DataGridHeader : TemplatedControl
{
    private const string CellsPart = "PART_Cells";
    private const string ScrollerPart = "PART_Scroller";

    public static readonly StyledProperty<GridColumns?> ColumnsProperty =
        AvaloniaProperty.Register<DataGridHeader, GridColumns?>(nameof(Columns));

    private readonly List<DataGridHeaderCell> cells = [];
    private readonly List<ColumnDivider> dividers = [];

    private GridCells? panel;

    /// <summary>Raised when an edge is double clicked. Only the grid can measure the cells.</summary>
    internal event EventHandler<GridColumn>? Fitting;

    /// <summary>The title drawing a column, or null when it has none.</summary>
    internal DataGridHeaderCell? CellFor(GridColumn column) =>
        cells.FirstOrDefault(cell => ReferenceEquals(cell.Column, column));

    public GridColumns? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Where the sideways offset is written. The grid drives it from the body.</summary>
    internal ScrollViewer? Scroller { get; private set; }

    /// <summary>Builds a title and an edge per column.</summary>
    internal void Rebuild()
    {
        if (panel is null)
        {
            return;
        }

        panel.Children.Clear();
        cells.Clear();
        dividers.Clear();

        if (Columns is not { } columns)
        {
            return;
        }

        foreach (var column in columns)
        {
            var cell = new DataGridHeaderCell { Column = column };

            GridCells.SetColumn(cell, column);
            cells.Add(cell);
            panel.Children.Add(cell);
        }

        // After every title, so an edge is never covered by the title on its right and
        // the whole reach stays grabbable.
        foreach (var column in columns)
        {
            var divider = new ColumnDivider();

            divider.Fit += (sender, _) => Fitting?.Invoke(
                this,
                GridCells.GetColumn((Control)sender!)!);
            divider.Follow(columns, column);
            GridCells.SetColumn(divider, column);
            GridCells.SetIsDivider(divider, true);

            dividers.Add(divider);
            panel.Children.Add(divider);
        }

        Relayout();
    }

    /// <summary>Lays the titles out again after the column widths have moved.</summary>
    internal void Relayout()
    {
        foreach (var cell in cells)
        {
            cell.IsVisible = cell.Column?.IsVisible ?? false;
        }

        foreach (var divider in dividers)
        {
            var column = GridCells.GetColumn(divider);

            divider.IsVisible = column is { IsVisible: true }
                && Columns is { } owner
                && (owner.CanResize(column) || owner.CanFit(column));
        }

        panel?.InvalidateMeasure();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        panel = e.NameScope.Find<GridCells>(CellsPart);
        Scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);

        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnsProperty)
        {
            Rebuild();
        }
    }
}
