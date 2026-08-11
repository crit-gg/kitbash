using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The grab area on a column's trailing edge. It draws nothing at rest, since the design
/// rules columns off with no vertical line, and it reaches either side of the edge the way
/// the splitter does, because one pixel cannot be grabbed.
/// </summary>
public class ColumnDivider : TemplatedControl
{
    private GridColumn? column;
    private GridColumns? columns;
    private double startWidth;
    private double startX;
    private bool dragging;

    /// <summary>Raised when the edge is double clicked, which fits the column to its cells.</summary>
    internal event EventHandler? Fit;

    public ColumnDivider()
    {
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    /// <summary>The column this edge belongs to, and where its widths are written.</summary>
    internal void Follow(GridColumns owner, GridColumn edge)
    {
        columns = owner;
        column = edge;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (column is null || columns is null || Parent is not Visual reference)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // The grid works the width out from the cells, since only it knows which rows are
        // realised and a column cannot measure what is not there.
        if (e.ClickCount == 2)
        {
            if (columns.CanFit(column))
            {
                Fit?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
            return;
        }

        if (!columns.CanResize(column))
        {
            return;
        }

        dragging = true;
        startWidth = column.ActualWidth;
        startX = e.GetPosition(reference).X;

        PseudoClasses.Set(":dragging", true);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!dragging || column is null || columns is null || Parent is not Visual reference)
        {
            return;
        }

        // Measured against the panel rather than against this control, since the edge
        // travels under the pointer as the column grows and a delta read from a moving
        // element would feed itself.
        columns.SetWidth(column, startWidth + (e.GetPosition(reference).X - startX));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!dragging)
        {
            return;
        }

        dragging = false;
        PseudoClasses.Set(":dragging", false);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        dragging = false;
        PseudoClasses.Set(":dragging", false);
    }
}
