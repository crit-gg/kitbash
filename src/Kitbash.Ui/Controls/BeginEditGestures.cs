namespace Kitbash.Ui.Controls;

/// <summary>
/// What opens an editor. <see cref="None"/> is the read only grid, so there is no separate
/// switch for that.
/// </summary>
[Flags]
public enum BeginEditGestures
{
    /// <summary>Nothing opens an editor, whatever a column declares.</summary>
    None = 0,

    /// <summary>A double click in the cell.</summary>
    DoubleTap = 1,

    /// <summary>F2 on the current cell, which is the Windows convention.</summary>
    F2 = 2,

    /// <summary>Enter on the current cell.</summary>
    Enter = 4,

    /// <summary>
    /// A printable key on the current cell, which takes that character as the first one.
    /// What a person doing data entry expects, and also a stray keystroke starting an edit.
    /// </summary>
    TextInput = 8,
}
