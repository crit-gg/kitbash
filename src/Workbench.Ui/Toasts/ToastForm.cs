namespace Workbench.Ui.Toasts;

/// <summary>
/// How much of the anatomy a toast draws.
/// </summary>
/// <remarks>
/// The grouped form is not here. It is not a shape a caller picks, it is what a card
/// becomes when the same toast fires again, and it is any of these with a count on it.
/// </remarks>
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
