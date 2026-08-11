namespace Kitbash.Ui.Controls;

/// <summary>
/// What a person may do to the columns. Each one needs the column to allow it as well, so a
/// grid saying yes and a column saying no still means no.
/// </summary>
[Flags]
public enum ColumnGestures
{
    None = 0,

    /// <summary>Dragging the edge between two titles. On by default.</summary>
    Resize = 1,

    /// <summary>A double click on that edge, which fits the column to what is on screen. On by default.</summary>
    FitToContents = 2,

    /// <summary>Dragging a title to move the column.</summary>
    Reorder = 4,

    /// <summary>Taking a column off the grid.</summary>
    Hide = 8,

    /// <summary>Holding a column against the left edge while the rest scroll under it.</summary>
    Pin = 16,

    /// <summary>Shift and a click on a title, which adds a sort key rather than replacing one.</summary>
    MultiSort = 32,
}
