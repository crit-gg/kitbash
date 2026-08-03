namespace Kitbash.Ui.Toasts;

/// <summary>
/// What a toast is saying. Four semantic tiers and one busy tier.
/// </summary>
public enum ToastTier
{
    /// <summary>Something happened that is worth knowing and needs nothing.</summary>
    Info,

    /// <summary>Something finished and it worked.</summary>
    Ok,

    /// <summary>Something finished and part of it did not.</summary>
    Warn,

    /// <summary>Something did not happen. Stays until it is dismissed.</summary>
    Error,

    /// <summary>Work is still running. Stays until it is dismissed.</summary>
    Busy,
}
