namespace Workbench.Ui.Toasts;

/// <summary>
/// How much of the anatomy a toast draws.
/// </summary>
public enum ToastForm
{
    /// <summary>
    /// The full card: a status mark, a title, an optional body, up to two actions and a
    /// bar along the foot.
    /// </summary>
    Card,

    /// <summary>
    /// One line and a glyph for an acknowledgement. No body, no actions, no bar, and it
    /// is only as wide as its words.
    /// </summary>
    Compact,

    /// <summary>
    /// A full width bar for a reversible edit: what happened, one way to undo it, and a
    /// way to close. The first action is the undo and any others are ignored.
    /// </summary>
    Undo,
}
