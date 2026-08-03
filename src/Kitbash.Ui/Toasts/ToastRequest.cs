namespace Kitbash.Ui.Toasts;

/// <summary>
/// What a caller asks for. Everything except the title has a default, so the shortest
/// useful request is one line.
/// </summary>
/// <example>
/// <code>
/// toasts.Show(new ToastRequest { Tier = ToastTier.Ok, Title = "Saved 12 recipes" });
/// </code>
/// </example>
public sealed class ToastRequest
{
    /// <summary>
    /// The line a person reads first. One or two lines, and it wraps rather than being
    /// cut off, so it can be a sentence.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>The detail needed to act on the title. Optional, and the card form alone draws it.</summary>
    public string? Body { get; init; }

    public ToastTier Tier { get; init; } = ToastTier.Info;

    public ToastForm Form { get; init; } = ToastForm.Card;

    /// <summary>Which of the eight regions. Bottom right unless it is said otherwise.</summary>
    public ToastAnchor Anchor { get; init; } = ToastAnchor.BottomRight;

    /// <summary>
    /// At most two, and more than two is refused rather than trimmed. A third button on
    /// a card that closes itself is a decision nobody has time to make.
    /// </summary>
    public IReadOnlyList<ToastAction> Actions { get; init; } = [];

    /// <summary>
    /// How long it stays. Null takes the default for the tier, which is four seconds,
    /// eight when there is an action, and indefinite for an error or anything busy.
    /// </summary>
    public TimeSpan? Dwell { get; init; }

    /// <summary>
    /// How far along the work is, from 0 to 1, for a toast that reports progress. Null
    /// means there is no work to report. Setting it takes the bar over from the timer,
    /// so the toast stays until whatever raised it dismisses it.
    /// </summary>
    public double? Progress { get; init; }

    /// <summary>
    /// What counts as the same toast firing again, so a repeat collapses onto it. Null
    /// derives a key from the tier, the form and the title. Set it to keep two toasts
    /// apart when their titles match.
    /// </summary>
    public string? GroupKey { get; init; }
}
