using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Projects;

/// <summary>
/// What an app says about one entry beyond its name and its path. The store knows none
/// of this, so an app answers it per row.
/// </summary>
public sealed record ProjectFacts
{
    /// <summary>
    /// The letter on the tile. One or two characters. Blank takes the first letter of
    /// the name.
    /// </summary>
    public string Mark { get; init; } = string.Empty;

    /// <summary>
    /// The tile's tone. Neutral is the plain grey square, and the other tiers are what
    /// give a list something to aim at. Blank takes a tone picked from the path, so a
    /// list is varied without an app choosing per row.
    /// </summary>
    public BadgeTier? MarkTier { get; init; }

    /// <summary>
    /// The one place state appears. An engine version, an asset count, read only,
    /// offline, a conflict. Blank draws no chip.
    /// </summary>
    public string Chip { get; init; } = string.Empty;

    public BadgeTier ChipTier { get; init; } = BadgeTier.Neutral;

    /// <summary>A glyph inside the chip, for a chip that reports a problem.</summary>
    public IconGlyph? ChipGlyph { get; init; }

    /// <summary>
    /// The entry cannot be opened as it stands. The path drops to muted and the row
    /// menu offers to locate it rather than to open it. Set this for a reason of the
    /// app's own. A path that is not on disk already counts.
    /// </summary>
    public bool IsBroken { get; init; }
}
