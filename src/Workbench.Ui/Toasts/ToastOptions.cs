namespace Workbench.Ui.Toasts;

/// <summary>
/// Every number the toast service behaves by, in one place. Registered as an instance,
/// so an application that wants a different pace registers its own before calling
/// <c>AddWorkbenchToasts</c>.
/// </summary>
public sealed class ToastOptions
{
    /// <summary>
    /// How many a region shows at once. Everything past this is held back and counted,
    /// rather than pushing the stack until it fills the window.
    /// </summary>
    public int VisiblePerRegion { get; init; } = 3;

    /// <summary>How long an acknowledgement stays.</summary>
    public TimeSpan Dwell { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long a toast with an action stays. Longer, because it has to be read and then
    /// decided on.
    /// </summary>
    public TimeSpan DwellWithAction { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How long a dismissed toast is kept so it can fade rather than vanish. The card's
    /// exit animation is this long, and the two are one number on purpose.
    /// </summary>
    public TimeSpan Exit { get; init; } = TimeSpan.FromMilliseconds(120);

    /// <summary>
    /// How often a running timer is advanced. It decides how smoothly the bar drains and
    /// nothing else, since it only runs while something is counting down.
    /// </summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// A toast firing again collapses onto the one already showing and counts, rather
    /// than pushing another card. Off means every one gets its own card.
    /// </summary>
    public bool Groups { get; init; } = true;
}
