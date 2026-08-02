namespace Workbench.Ui.Controls;

/// <summary>
/// What an alert is saying. The four semantic tiers, and no busy tier.
/// </summary>
/// <remarks>
/// An alert describes the state of what is on screen, and work still running is not a
/// state of the content. That is a toast, which is why <c>ToastTier</c> has a fifth
/// member and this does not.
/// </remarks>
public enum AlertTier
{
    /// <summary>Worth knowing before editing what is here.</summary>
    Info,

    /// <summary>The condition is good and saying so is worth the room.</summary>
    Ok,

    /// <summary>Something here will not do what it looks like it does.</summary>
    Warn,

    /// <summary>Something here cannot be done until this is fixed.</summary>
    Error,
}
