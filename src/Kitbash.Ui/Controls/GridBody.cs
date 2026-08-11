using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What both grids keep about where the keyboard is and what is being edited. It sits here
/// rather than in either grid, so a tree grid navigates and edits exactly the way a flat
/// grid does.
/// </summary>
/// <param name="owner">The grid. Its selected row is the row the current cell is in.</param>
/// <param name="columns">The columns, which is what left and right move across.</param>
internal sealed class GridBody(ListBox owner, GridColumns columns)
{
    private DataGridCell? editing;
    private object? edited;
    private GridColumn? current;

    /// <summary>The item whose row transaction is open, or null when none is.</summary>
    private object? open;

    /// <summary>The far corner of the block, or nothing when the block is the anchor alone.</summary>
    private int edgeRow = -1;
    private GridColumn? edgeColumn;

    /// <summary>How far a fill has been dragged, or nothing when one is not being dragged.</summary>
    private int fillRow = -1;
    private GridColumn? fillColumn;

    /// <summary>The block the footer was last told about, so it is only worked out on a move.</summary>
    private (int Top, int Bottom, int Left, int Right) told = (-1, -1, -1, -1);

    /// <summary>The cell being edited, or null when none is.</summary>
    public DataGridCell? EditingCell => editing;

    /// <summary>Which column the keyboard is in. The row is whichever one is selected.</summary>
    public GridColumn? CurrentColumn => current;

    /// <summary>What opens an editor. The grid holds the property and hands it over.</summary>
    public BeginEditGestures Gestures { get; set; } = BeginEditGestures.DoubleTap
        | BeginEditGestures.F2
        | BeginEditGestures.Enter;

    /// <summary>What an edit is a transaction over. The grid holds the property.</summary>
    public GridEditUnit Unit { get; set; } = GridEditUnit.Cell;

    /// <summary>What a person may do to the values. The grid holds the property.</summary>
    public CellActions Actions { get; set; } = CellActions.Copy;

    /// <summary>What a person picks. The grid holds the property.</summary>
    public GridSelectionUnit Selection { get; set; } = GridSelectionUnit.Row;


    /// <summary>What the footer says about a live block, or empty when there is not one.</summary>
    public string RangeText { get; private set; } = string.Empty;

    /// <summary>Raised when the block moves, so a footer can say something else about it.</summary>
    public event EventHandler? RangeChanged;

    /// <summary>Whether a drag that started on a cell is still going.</summary>
    public bool Dragging { get; private set; }

    /// <summary>Whether the handle at the corner of the block is being dragged.</summary>
    public bool Filling { get; private set; }

    /// <summary>Whether a block of cells is a thing here at all.</summary>
    private bool Blocks => Selection == GridSelectionUnit.Cell;

    /// <summary>Which rows hold changes that are not saved. Null marks none of them.</summary>
    public Func<object, bool>? Modified { get; set; }

    /// <summary>Whether anything is being marked at all, so a row can stop early.</summary>
    public bool Marks => Modified is not null;

    /// <summary>Whether a row transaction is open. False whenever the unit is the cell.</summary>
    public bool IsRowEditing => open is not null;

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

        if (Unit == GridEditUnit.Row)
        {
            // A row transaction spans every cell in one row, so moving to another row is
            // what closes the one already open.
            if (!ReferenceEquals(open, edited))
            {
                CommitRow();
                open = edited;
                (open as IEditableObject)?.BeginEdit();
            }
        }
        else
        {
            (edited as IEditableObject)?.BeginEdit();
        }

        cell.IsEditing = true;
        RowOf(cell)?.SetEditing(true);
    }

    /// <summary>
    /// Keeps what was typed and closes the editor. A row transaction stays open, since it
    /// closes when the edit leaves the row rather than when a cell does.
    /// </summary>
    public void CommitEdit() => End(commit: true);

    /// <summary>
    /// Closes the editor and puts the value back. With a row transaction open there is no
    /// per field undo to call on, so the whole row goes back.
    /// </summary>
    public void CancelEdit()
    {
        End(commit: false);
        CancelRow();
    }

    /// <summary>
    /// Keeps what was typed and moves down a row, staying in the same column. What a person
    /// doing data entry expects from Enter, and it is what closes a row transaction.
    /// </summary>
    public void CommitAndStepDown()
    {
        CommitEdit();

        var below = Next(owner.SelectedIndex, 1);

        if (below >= 0 && current is { } column)
        {
            To(below, column);
        }
        else
        {
            CommitRow();
        }
    }

    /// <summary>Ends the row transaction, keeping what was typed into it.</summary>
    public void CommitRow()
    {
        var item = open;

        open = null;
        (item as IEditableObject)?.EndEdit();
    }

    /// <summary>Ends the row transaction, putting every field back.</summary>
    public void CancelRow()
    {
        var item = open;

        open = null;
        (item as IEditableObject)?.CancelEdit();
    }

    /// <summary>
    /// The selection has moved. A row transaction over a row nothing is editing any more is
    /// closed here, which is the only thing that closes it in ordinary use.
    /// </summary>
    public void OnSelectionMoved()
    {
        if (open is not null && editing is null)
        {
            CommitRow();
        }
    }

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
        var collapsed = edgeRow >= 0;

        edgeRow = -1;
        edgeColumn = null;

        if (ReferenceEquals(current, column) && !collapsed)
        {
            return;
        }

        current = column;
        Refresh();
    }

    /// <summary>A press on a cell may be the start of a drag over the cells beside it.</summary>
    public void BeginDrag() => Dragging = Blocks;

    /// <summary>The button came up, so a drag over cells is over.</summary>
    public void EndDrag() => Dragging = false;

    /// <summary>A press on the handle starts a fill out of the block.</summary>
    public void BeginFill() => Filling = Blocks && Actions.HasFlag(CellActions.Fill);

    /// <summary>
    /// Takes the reach of the fill out to a cell. Nothing is written while the pointer is
    /// down, so the preview says what would land rather than landing it.
    /// </summary>
    public void PreviewFill(DataGridCell cell, GridColumn column)
    {
        if (!Filling || cell.FindAncestorOfType<ListBoxItem>() is not { } container)
        {
            return;
        }

        fillRow = owner.IndexFromContainer(container);
        fillColumn = column;

        Refresh();
    }

    /// <summary>
    /// The button came up, so the block's values are repeated over what the fill reached and
    /// the block grows to take it in.
    /// </summary>
    public void EndFill()
    {
        if (!Filling)
        {
            return;
        }

        Filling = false;

        if (fillRow < 0 || fillColumn is null)
        {
            return;
        }

        var reachable = columns.Reachable;
        var from = Block(reachable, reach: false);
        var over = Block(reachable);

        fillRow = -1;
        fillColumn = null;

        if (Repeat(reachable, from, over))
        {
            // The block takes in what it filled, which is where a person carries on from.
            edgeRow = over.Bottom;
            edgeColumn = reachable[Math.Min(over.Right, reachable.Count - 1)];
        }

        Refresh();
    }

    /// <summary>
    /// Writes the block's own values over everything the fill reached, repeating them the
    /// way a paste repeats into a block that divides evenly.
    /// </summary>
    private bool Repeat(
        IReadOnlyList<GridColumn> reachable,
        (int Top, int Bottom, int Left, int Right) from,
        (int Top, int Bottom, int Left, int Right) over)
    {
        var deep = from.Bottom - from.Top + 1;
        var wide = from.Right - from.Left + 1;
        var held = new List<List<string?>>();

        for (var down = 0; down < deep; down++)
        {
            if (Held(from.Top + down) is not { } item)
            {
                return false;
            }

            held.Add([.. Enumerable.Range(0, wide).Select(across => Text(reachable, from.Left + across, item))]);
        }

        for (var index = over.Top; index <= over.Bottom; index++)
        {
            if (Held(index) is not { } item)
            {
                return false;
            }

            for (var at = over.Left; at <= over.Right; at++)
            {
                if (at >= reachable.Count || !reachable[at].CanWrite)
                {
                    return false;
                }
            }
        }

        for (var index = over.Top; index <= over.Bottom; index++)
        {
            if (Held(index) is not { } item)
            {
                continue;
            }

            var inside = index >= from.Top && index <= from.Bottom;

            (item as IEditableObject)?.BeginEdit();

            for (var at = over.Left; at <= over.Right; at++)
            {
                if (inside && at >= from.Left && at <= from.Right)
                {
                    continue;
                }

                reachable[at].Write!(item, held[(index - over.Top) % deep][(at - over.Left) % wide]);
            }

            (item as IEditableObject)?.EndEdit();
        }

        return true;
    }

    private static string? Text(IReadOnlyList<GridColumn> reachable, int at, object item) =>
        at < reachable.Count ? reachable[at].ValueOf(item)?.ToString() : null;

    /// <summary>
    /// Takes the block out to a cell, keeping the anchor where it is. In row units there is
    /// no block, so the press moves the keyboard instead and the list extends its own
    /// selection.
    /// </summary>
    public void ExtendTo(DataGridCell cell, GridColumn column)
    {
        if (Selection != GridSelectionUnit.Cell)
        {
            SetCurrent(column);
            return;
        }

        if (cell.FindAncestorOfType<ListBoxItem>() is not { } container)
        {
            return;
        }

        edgeRow = owner.IndexFromContainer(container);
        edgeColumn = column;

        Refresh();
    }

    /// <summary>
    /// The block as row and column bounds, which is the anchor alone unless a range has been
    /// taken out from it.
    /// </summary>
    private (int Top, int Bottom, int Left, int Right) Block(IReadOnlyList<GridColumn> reachable, bool reach = true)
    {
        var row = owner.SelectedIndex;
        var at = Math.Max(0, At(reachable, current));
        var block = (Top: row, Bottom: row, Left: at, Right: at);

        if (Selection == GridSelectionUnit.Cell && edgeRow >= 0 && edgeColumn is not null)
        {
            var far = Math.Max(0, At(reachable, edgeColumn));

            block = (Math.Min(row, edgeRow), Math.Max(row, edgeRow), Math.Min(at, far), Math.Max(at, far));
        }

        if (!reach || fillRow < 0 || fillColumn is null)
        {
            return block;
        }

        var over = Math.Max(0, At(reachable, fillColumn));

        return (
            Math.Min(block.Top, fillRow),
            Math.Max(block.Bottom, fillRow),
            Math.Min(block.Left, over),
            Math.Max(block.Right, over));
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

        var reachable = columns.Reachable;
        var block = Block(reachable);
        var held = Filling ? Block(reachable, reach: false) : block;
        var handles = Blocks && Actions.HasFlag(CellActions.Fill);
        var wanted = CellAt(owner.SelectedIndex, current);

        if (block != told)
        {
            told = block;
            RangeText = Describe(block, reachable);
            RangeChanged?.Invoke(this, EventArgs.Empty);
        }

        foreach (var container in owner.GetRealizedContainers())
        {
            if (container is not IGridRowLayout row)
            {
                continue;
            }

            var index = owner.IndexFromContainer(container);
            var down = index >= block.Top && index <= block.Bottom;
            var here = false;

            foreach (var cell in row.Cells)
            {
                var at = At(reachable, cell.Column);
                var inside = down && at >= block.Left && at <= block.Right;
                var mine = ReferenceEquals(cell, wanted);

                var reached = Filling
                    && inside
                    && (index < held.Top || index > held.Bottom || at < held.Left || at > held.Right);

                cell.IsCurrent = mine;

                // The anchor keeps the plain surface, so the one cell that still takes
                // typing is marked by having no wash rather than by a second ring.
                cell.IsInRange = inside && !mine && !reached;
                cell.IsFilling = reached;
                cell.HasHandle = handles && index == held.Bottom && at == held.Right;

                cell.RangeEdges = inside
                    ? new Thickness(
                        at == block.Left ? 1 : 0,
                        index == block.Top ? 1 : 0,
                        at == block.Right ? 1 : 0,
                        index == block.Bottom ? 1 : 0)
                    : default;

                here |= mine;
            }

            row.SetCurrent(here);
            Mark(row);
        }
    }

    /// <summary>How many cells the block holds, and what they add up to when they are numbers.</summary>
    private string Describe((int Top, int Bottom, int Left, int Right) block, IReadOnlyList<GridColumn> reachable)
    {
        if (!Blocks || (block.Top == block.Bottom && block.Left == block.Right))
        {
            return string.Empty;
        }

        var right = Math.Min(block.Right, reachable.Count - 1);
        var wide = right - block.Left + 1;
        var rows = 0;

        for (var index = block.Top; index <= block.Bottom; index++)
        {
            rows += Held(index) is null ? 0 : 1;
        }

        var cells = rows * wide;

        if (cells == 0)
        {
            return string.Empty;
        }

        // Past this the sum costs more than it is worth on every step of a growing block,
        // and a person picking that many cells is picking rather than adding up.
        if (cells > 50_000)
        {
            return $"{cells:N0} cells";
        }

        var numbers = 0;
        var sum = 0d;

        for (var index = block.Top; index <= block.Bottom; index++)
        {
            if (Held(index) is not { } item)
            {
                continue;
            }

            for (var at = block.Left; at <= right; at++)
            {
                if (Number(reachable[at].ValueOf(item)) is { } value)
                {
                    numbers++;
                    sum += value;
                }
            }
        }

        return numbers == 0
            ? $"{cells:N0} cells"
            : $"{cells:N0} cells, sum {sum:N2}, avg {sum / numbers:N2}";
    }

    /// <summary>A value as a number, or null when it is not one.</summary>
    private static double? Number(object? value) => value switch
    {
        null or bool or char => null,
        string text => double.TryParse(text, out var parsed) ? parsed : null,
        IConvertible number => Convert.ToDouble(number, System.Globalization.CultureInfo.CurrentCulture),
        _ => null,
    };

    /// <summary>Says whether one row holds unsaved changes, which its item decides.</summary>
    public void Mark(IGridRowLayout row) =>
        row.SetModified(Modified is { } modified && row.Held is { } item && modified(item));

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
            Key.Left when shift && !control && Blocks => Stretch(0, -1, reachable),
            Key.Right when shift && !control && Blocks => Stretch(0, 1, reachable),
            Key.Up when shift && !control && Blocks => Stretch(-1, 0, reachable),
            Key.Down when shift && !control && Blocks => Stretch(1, 0, reachable),
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
            Key.C when control && !shift && Actions.HasFlag(CellActions.Copy) => Copy(),
            Key.X when control && !shift && Actions.HasFlag(CellActions.Cut) => Cut(),
            Key.V when control && !shift && Actions.HasFlag(CellActions.Paste) => Paste(),
            Key.Delete when !control && !shift && Actions.HasFlag(CellActions.Clear) => Clear(),
            _ => false,
        };
    }

    /// <summary>
    /// Writes what is picked to the clipboard as text a spreadsheet reads. Nothing picked
    /// writes the row the keyboard is on, since that is the one a person means.
    /// </summary>
    public bool Copy()
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is not { } clipboard)
        {
            return false;
        }

        var reachable = columns.Reachable;

        if (Blocks)
        {
            if (BlockText(reachable, Block(reachable)) is not { } block)
            {
                return false;
            }

            _ = clipboard.SetTextAsync(block);
            return true;
        }

        var picked = Picked();

        if (picked.Count == 0)
        {
            return false;
        }

        // A column that says nothing about an item, such as one holding a picker, is left
        // out rather than written as an empty field somebody has to delete.
        var written = reachable.Where(column => column.HasValue).ToList();

        _ = clipboard.SetTextAsync(new GridClipboard().Write(picked, written));
        return true;
    }

    /// <summary>
    /// Writes the block out and then empties it. It copies the same cells it clears, which
    /// is why it does not go through <see cref="Copy"/>, since that writes whole rows when
    /// the unit is the row.
    /// </summary>
    public bool Cut()
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is not { } clipboard)
        {
            return false;
        }

        var reachable = columns.Reachable;
        var text = BlockText(reachable, Block(reachable));

        if (text is null || !Clear())
        {
            return false;
        }

        _ = clipboard.SetTextAsync(text);
        return true;
    }

    /// <summary>Empties every cell that is picked.</summary>
    public bool Clear() => Put((_, _) => null);

    /// <summary>
    /// Reads the clipboard and writes it in from the anchor. The read is asynchronous, so
    /// this says the key was taken rather than whether anything landed.
    /// </summary>
    public bool Paste()
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is not { } clipboard)
        {
            return false;
        }

        _ = Land(clipboard);
        return true;
    }

    private async Task Land(Avalonia.Input.Platform.IClipboard clipboard)
    {
        if (await clipboard.TryGetTextAsync() is not { Length: > 0 } text)
        {
            return;
        }

        var arrived = new GridClipboard().Read(text);

        if (arrived.Count == 0)
        {
            return;
        }

        var deep = arrived.Count;
        var wide = arrived.Max(line => line.Count);
        var reachable = columns.Reachable;
        var block = Block(reachable);
        var down = block.Bottom - block.Top + 1;
        var across = block.Right - block.Left + 1;

        // A block bigger than one cell takes the paste repeated to fill it, but only when
        // it divides evenly. Anything else lands at the anchor in the shape it arrived in.
        var fills = (down > 1 || across > 1) && down % deep == 0 && across % wide == 0;

        // Both ways round, since filling a block repeats across as well as down. A line
        // shorter than the widest one leaves those cells empty rather than repeating itself.
        Put(
            (row, column) => arrived[row % deep] is { } line && column % wide < line.Count
                ? line[column % wide]
                : null,
            fills ? down : deep,
            fills ? across : wide);
    }

    /// <summary>
    /// Writes into the cells that are picked, or into a shape of that size from the anchor.
    /// **Nothing is written at all unless every cell it would touch can take a value**, so a
    /// paste over a column that is read only is refused rather than half applied.
    /// </summary>
    private bool Put(Func<int, int, string?> value, int deep = 0, int across = 0)
    {
        var reachable = columns.Reachable;
        var block = Block(reachable);

        var rows = deep > 0 ? deep : block.Bottom - block.Top + 1;
        var span = across > 0 ? across : block.Right - block.Left + 1;
        var items = new List<object>();

        for (var step = 0; step < rows; step++)
        {
            if (Held(block.Top + step) is not { } item)
            {
                return false;
            }

            items.Add(item);
        }

        var written = new List<GridColumn>();

        for (var step = 0; step < span; step++)
        {
            var at = block.Left + step;

            if (at >= reachable.Count || !reachable[at].CanWrite)
            {
                return false;
            }

            written.Add(reachable[at]);
        }

        if (items.Count == 0 || written.Count == 0)
        {
            return false;
        }

        foreach (var (item, row) in items.Select((item, row) => (item, row)))
        {
            (item as IEditableObject)?.BeginEdit();

            for (var column = 0; column < written.Count; column++)
            {
                written[column].Write!(item, value(row, column));
            }

            (item as IEditableObject)?.EndEdit();
        }

        return true;
    }

    /// <summary>
    /// The block as text, or null when it holds no rows. Every column it covers goes out,
    /// whether or not it says anything, since a person who picked it meant it and a blank
    /// field keeps the shape of what was picked.
    /// </summary>
    private string? BlockText(IReadOnlyList<GridColumn> reachable, (int Top, int Bottom, int Left, int Right) block)
    {
        var rows = new List<object>();

        for (var index = block.Top; index <= block.Bottom; index++)
        {
            if (Held(index) is { } item)
            {
                rows.Add(item);
            }
        }

        return rows.Count == 0
            ? null
            : new GridClipboard().Write(rows, [.. reachable.Skip(block.Left).Take(block.Right - block.Left + 1)]);
    }

    /// <summary>The items copy works over, in the order the rows are drawn in.</summary>
    private List<object> Picked()
    {
        var chosen = new HashSet<object>(
            owner.SelectedItems?.Cast<object>() ?? [],
            ReferenceEqualityComparer.Instance);

        var found = new List<object>();

        for (var index = 0; index < owner.ItemsView.Count; index++)
        {
            if (Held(index) is not { } item)
            {
                continue;
            }

            if (chosen.Count == 0 ? index == owner.SelectedIndex : chosen.Contains(owner.ItemsView[index]!))
            {
                found.Add(item);
            }
        }

        return found;
    }

    /// <summary>What a row at an index stands for, or null when it is a group heading.</summary>
    private object? Held(int index) => index < 0 || index >= owner.ItemsView.Count ? null : owner.ItemsView[index] switch
    {
        GridRow { IsGroup: false, Item: { } item } => item,
        TreeRow { Item: { } item } => item,
        _ => null,
    };

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

    /// <summary>
    /// Takes the block one step further out, leaving the anchor where it is. It answers the
    /// arrow itself so the list does not also extend the rows it has picked.
    /// </summary>
    private bool Stretch(int down, int across, IReadOnlyList<GridColumn> reachable)
    {
        var row = edgeRow >= 0 ? edgeRow : owner.SelectedIndex;
        var at = Math.Max(0, At(reachable, edgeColumn ?? current));

        if (row < 0)
        {
            return false;
        }

        var below = down == 0 ? row : Next(row, down);

        edgeRow = below >= 0 ? below : row;
        edgeColumn = reachable[Math.Clamp(at + across, 0, reachable.Count - 1)];

        Refresh();
        return true;
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

        // Under a row transaction the item is told nothing here. The transaction is what
        // ends it, so a cell closing leaves the row still open.
        if (Unit == GridEditUnit.Cell)
        {
            if (commit)
            {
                (item as IEditableObject)?.EndEdit();
            }
            else
            {
                (item as IEditableObject)?.CancelEdit();
            }
        }

        cell.IsEditing = false;
        RowOf(cell)?.SetEditing(false);
    }
}
