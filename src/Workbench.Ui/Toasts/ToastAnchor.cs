namespace Workbench.Ui.Toasts;

/// <summary>
/// Where a toast region sits. Eight of them, and all eight can be occupied at once.
/// </summary>
/// <remarks>
/// Each region owns its own stack, its own dwell timers and its own limit, so a card
/// appearing top left never reflows the bottom right stack. That is what lets a tool
/// panel report its own progress while the window reports a save.
/// <para>
/// Bottom right is first so a request that says nothing lands there, which is the
/// default the design gives. A toast about something visible elsewhere names the region
/// nearest that thing instead.
/// </para>
/// </remarks>
public enum ToastAnchor
{
    BottomRight,
    BottomCenter,
    BottomLeft,
    LeftCenter,
    RightCenter,
    TopLeft,
    TopCenter,
    TopRight,
}
