namespace Workbench.Ui.Controls;

/// <summary>
/// The tiers a <see cref="StatusPill"/> can read as.
/// </summary>
/// <remarks>
/// These are tiers, not meanings. The design shows five pills naming five states of a
/// file, which is content rather than library, so the pill takes the tier and the
/// caller supplies the word. Ok, Modified and Error come from the semantic table.
/// Accent and Neutral are the two remaining tiers the palette already carries, for a
/// state that is called out without being good or bad, and for one that is neither.
/// </remarks>
public enum PillStatus
{
    Neutral,
    Ok,
    Modified,
    Error,
    Accent,
}
