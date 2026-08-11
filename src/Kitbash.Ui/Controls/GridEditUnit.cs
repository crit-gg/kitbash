namespace Kitbash.Ui.Controls;

/// <summary>
/// What an edit is a transaction over. It only means anything for an item that implements
/// <see cref="System.ComponentModel.IEditableObject"/> properly.
/// </summary>
public enum GridEditUnit
{
    /// <summary>
    /// One cell. The item is told to begin when the cell opens and to end when it closes,
    /// so each field stands on its own.
    /// </summary>
    Cell,

    /// <summary>
    /// The whole row. The item is told to begin at the first cell touched and to end when
    /// the edit leaves the row, so a rule over three fields can be checked once they all
    /// hold values, and cancelling puts every field back.
    /// </summary>
    Row,
}
