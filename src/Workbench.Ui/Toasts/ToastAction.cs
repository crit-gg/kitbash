namespace Workbench.Ui.Toasts;

/// <summary>
/// One thing a person can press on a toast. A description rather than a control, so a
/// view model names an action without knowing what draws it.
/// </summary>
/// <remarks>
/// There is no destructive kind here and there will not be one. A toast is read after
/// the fact and often out of the corner of an eye, so nothing on one may destroy
/// anything. Whatever is worth keeping is also in the log or the problem list, which is
/// where a destructive answer belongs.
/// </remarks>
/// <param name="Text">The word on the button. A verb, and short.</param>
/// <param name="Invoke">What pressing it does.</param>
public sealed record ToastAction(string Text, Action Invoke)
{
    /// <summary>
    /// Draws as the one accent button. Set it only when the action is safe to press
    /// without reading the rest of the card, since that is what an accent button says.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// The toast closes once the action has run. True by default, because a toast that
    /// stays after it has been answered is asking to be answered again.
    /// </summary>
    public bool Dismisses { get; init; } = true;
}
