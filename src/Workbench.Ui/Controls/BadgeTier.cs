namespace Workbench.Ui.Controls;

/// <summary>
/// The tones a <see cref="Badge"/> can take.
/// </summary>
/// <remarks>
/// These are not <see cref="PillStatus"/>. A badge names something and a status pill
/// reports a state, so the two carry different tiers as well as different parts.
/// <see cref="Data"/> exists here and not there because a runtime or a format is a fact
/// about a thing rather than a judgement on it.
/// </remarks>
public enum BadgeTier
{
    Neutral,
    Ok,
    Modified,
    Error,
    Accent,
    Data,

    /// <summary>The purple tint. A pre release channel, and nothing else so far.</summary>
    Graph,
}
