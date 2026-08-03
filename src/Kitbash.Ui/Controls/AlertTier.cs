namespace Kitbash.Ui.Controls;

/// <summary>
/// What an alert is saying. The four semantic tiers, and no busy tier.
/// </summary>
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
