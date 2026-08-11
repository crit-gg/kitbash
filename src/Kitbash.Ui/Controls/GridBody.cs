using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What both grids keep about where the keyboard is and what is being edited. It sits here
/// rather than in either grid, so a tree grid navigates and edits exactly the way a flat
/// grid does.
/// </summary>
/// <param name="owner">The grid. Its selected row is the row the current cell is in.</param>
/// <param name="columns">The columns, which is what left and right move across.</param>
internal sealed class GridBody(SelectingItemsControl owner, GridColumns columns)
{
    private DataGridCell? editing;
    private object? edited;
    private GridColumn? current;

    /// <summary>The cell being edited, or null when none is.</summary>
    public DataGridCell? EditingCell => editing;

    /// <summary>Which column the keyboard is in. The row is whichever one is selected.</summary>
    public GridColumn? CurrentColumn => current;

    /// <summary>What opens an editor. The grid holds the property and hands it over.</summary>
    public BeginEditGestures Gestures { get; set; } = BeginEditGestures.DoubleTap
        | BeginEditGestures.F2
        | BeginEditGestures.Enter;

    /// <summary>
    /// The column that draws the caret, when the grid has one. Left and right belong to the
    /// hierarchy there and to the cells in every other column.
    /// </summary>
    public GridColumn? Hierarchy { get; set; }

    /// <summary>
    /// Puts a cell into its column's edit template. The item is told through
    /// <see cref="IEditableObject"/> when it implements it, which is what makes cancel able
    /// to put the old value back.
    /// </summary>
    public void BeginEdit(DataGridCell cell)
    {
        if (!cell.CanEdit || Gestures == BeginEditGestures.None || ReferenceEquals(cell, editing))
        {
            return;
        }

        CommitEdit();

        editing = cell;
        edited = cell.Content;
        (edited as IEditableObject)?.BeginEdit();

        cell.IsEditing = true;
        RowOf(cell)?.SetEditing(true);
    }

    /// <summary>Keeps what was typed and closes the editor.</summary>
    public void CommitEdit() => End(commit: true);

    /// <summary>Puts the old value back and closes the editor.</summary>
    public void CancelEdit() => End(commit: false);

    /// <summary>Whether the cell being edited is inside this container.</summary>
    public bool Holds(Control container) =>
        editing is { } open && ReferenceEquals(open.FindAncestorOfType<ListBoxItem>(), container);

    /// <summary>
    /// Opens the cell this point landed in, when there is one and it can be edited. Whether
    /// it did, so a caller can leave the gesture alone when it did not.
    /// </summary>
    public bool BeginEditAt(Visual source)
    {
        if (!Gestures.HasFlag(BeginEditGestures.DoubleTap))
        {
            return false;
        }

        if (source.FindAncestorOfType<DataGridCell>(includeSelf: true) is not { CanEdit: true } cell)
        {
            return false;
        }

        BeginEdit(cell);
        return true;
    }

    /// <summary>Moves the keyboard to a column, which a press on a cell does.</summary>
    public void SetCurrent(GridColumn? column)
    {
        if (ReferenceEquals(current, column))
        {
            return;
        }

        current = column;
        Refresh();
    }

    /// <summary>
    /// Marks the cell the keyboard is in and clears every other one. Called whenever the
    /// selection, the columns or the realised rows move, since a cell scrolled out of view
    /// loses its container and the one recycled in its place would keep the mark.
    /// </summary>
    public void Refresh()
    {
        // A row being picked puts the keyboard somewhere, so the first column takes it
        // until something moves it.
        if (current is null && owner.SelectedIndex >= 0)
        {
            current = columns.Reachable.FirstOrDefault();
        }

        var wanted = CellAt(owner.SelectedIndex, current);

        foreach (var container in owner.GetRealizedContainers())
        {
            if (container is not IGridRowLayout row)
            {
                continue;
            }

            var here = false;

            foreach (var cell in row.Cells)
            {
                var mine = ReferenceEquals(cell, wanted);

                cell.IsCurrent = mine;
                here |= mine;
            }

            row.SetCurrent(here);
        }
    }

    /// <summary>
    /// The keys that move the current cell or open an editor. Up and down are left to the
    /// list, which moves the row and takes the current cell with it, so this owns the
    /// column and the list owns the row.
    /// </summary>
    /// <returns>Whether the key was answered here.</returns>
    public bool OnKeyDown(KeyEventArgs e)
    {
        // While a cell is open the editor has the keys, and the cell itself answers Enter
        // and Escape.
        if (editing is not null)
        {
            return false;
        }

        var reachable = columns.Reachable;

        if (reachable.Count == 0)
        {
            return false;
        }

        current ??= reachable[0];

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        return e.Key switch
        {
            Key.Left when !control && !shift && !InHierarchy() => Step(-1, reachable),
            Key.Right when !control && !shift && !InHierarchy() => Step(1, reachable),
            Key.Home when control => To(FirstRow(), reachable[0]),
            Key.End when control => To(LastRow(), reachable[^1]),
            Key.Home when !shift => To(owner.SelectedIndex, reachable[0]),
            Key.End when !shift => To(owner.SelectedIndex, reachable[^1]),
            Key.Tab when !control => Hop(shift ? -1 : 1, reachable),
            Key.F2 when Gestures.HasFlag(BeginEditGestures.F2) => Open(),
            Key.Enter when Gestures.HasFlag(BeginEditGestures.Enter) && !control && !shift => Open(),
            Key.Space when shift && !control => Pick(),
            _ => false,
        };
    }

    /// <summary>
    /// A printable character opens the editor and is the first thing typed into it. The
    /// character reaches the editor on its own, since the key that produced it is left
    /// unhandled and the editor has the focus by the time it arrives.
    /// </summary>
    public bool OnTextInput(TextInputEventArgs e)
    {
        if (editing is not null
            || !Gestures.HasFlag(BeginEditGestures.TextInput)
            || string.IsNullOrEmpty(e.Text)
            || char.IsControl(e.Text[0]))
        {
            return false;
        }

        if (CellAt(owner.SelectedIndex, current) is not { CanEdit: true } cell)
        {
            return false;
        }

        BeginEdit(cell);
        return false;
    }

    private static IGridRowLayout? RowOf(DataGridCell cell) =>
        cell.FindAncestorOfType<ListBoxItem>() as IGridRowLayout;

    /// <summary>Whether the keyboard is in the column the hierarchy indents in.</summary>
    private bool InHierarchy() => Hierarchy is not null && ReferenceEquals(current, Hierarchy);

    /// <summary>The cell at a row and a column, or null when that row is not realised.</summary>
    private DataGridCell? CellAt(int index, GridColumn? column)
    {
        if (column is null || index < 0)
        {
            return null;
        }

        return owner.ContainerFromIndex(index) is IGridRowLayout row
            ? row.Cells.FirstOrDefault(cell => ReferenceEquals(cell.Column, column))
            : null;
    }

    /// <summary>Where a column comes among the ones a person can reach, or minus one.</summary>
    private static int At(IReadOnlyList<GridColumn> reachable, GridColumn? column)
    {
        for (var index = 0; index < reachable.Count; index++)
        {
            if (ReferenceEquals(reachable[index], column))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Moves the keyboard one column, stopping at either edge.</summary>
    private bool Step(int by, IReadOnlyList<GridColumn> reachable)
    {
        var at = Math.Max(0, At(reachable, current)) + by;

        if (at < 0 || at >= reachable.Count)
        {
            return false;
        }

        SetCurrent(reachable[at]);
        return true;
    }

    /// <summary>
    /// Moves the keyboard one cell, wrapping to the next row at the end of one. A grid that
    /// refuses to wrap sends a person to the mouse at the end of every row.
    /// </summary>
    private bool Hop(int by, IReadOnlyList<GridColumn> reachable)
    {
        var at = Math.Max(0, At(reachable, current)) + by;

        if (at >= 0 && at < reachable.Count)
        {
            SetCurrent(reachable[at]);
            return true;
        }

        var row = Next(owner.SelectedIndex, by);

        return row >= 0 && To(row, by > 0 ? reachable[0] : reachable[^1]);
    }

    /// <summary>Puts the keyboard on a row and a column, taking the selection with it.</summary>
    private bool To(int index, GridColumn column)
    {
        if (index < 0 || index >= owner.ItemsView.Count)
        {
            return false;
        }

        current = column;

        if (owner.SelectedIndex != index)
        {
            owner.SelectedIndex = index;
            owner.UpdateLayout();
            (owner.ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
        }

        Refresh();
        return true;
    }

    /// <summary>Opens the current cell, when there is one and its column allows it.</summary>
    private bool Open()
    {
        if (CellAt(owner.SelectedIndex, current) is not { CanEdit: true } cell)
        {
            return false;
        }

        BeginEdit(cell);
        return true;
    }

    /// <summary>Shift and space picks the row the keyboard is on.</summary>
    private bool Pick()
    {
        if (owner.SelectedIndex < 0)
        {
            return false;
        }

        (owner.ContainerFromIndex(owner.SelectedIndex) as ListBoxItem)?.SetCurrentValue(
            ListBoxItem.IsSelectedProperty,
            true);

        return true;
    }

    /// <summary>The next row a cell can sit in, stepping over a group heading.</summary>
    private int Next(int from, int by)
    {
        for (var at = from + by; at >= 0 && at < owner.ItemsView.Count; at += by)
        {
            if (Navigable(at))
            {
                return at;
            }
        }

        return -1;
    }

    private int FirstRow() => Navigable(0) ? 0 : Next(0, 1);

    private int LastRow()
    {
        var last = owner.ItemsView.Count - 1;

        return Navigable(last) ? last : Next(last, -1);
    }

    /// <summary>Whether a row holds cells at all. A group heading does not.</summary>
    private bool Navigable(int index) =>
        index >= 0
        && index < owner.ItemsView.Count
        && owner.ItemsView[index] is not GridRow { IsGroup: true };

    private void End(bool commit)
    {
        if (editing is null)
        {
            return;
        }

        var cell = editing;
        var item = edited;

        editing = null;
        edited = null;

        if (commit)
        {
            (item as IEditableObject)?.EndEdit();
        }
        else
        {
            (item as IEditableObject)?.CancelEdit();
        }

        cell.IsEditing = false;
        RowOf(cell)?.SetEditing(false);
    }
}
