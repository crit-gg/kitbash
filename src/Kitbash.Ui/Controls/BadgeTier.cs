namespace Kitbash.Ui.Controls;

/// <summary>
/// The tones a <see cref="Badge"/> can take.
/// </summary>
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
