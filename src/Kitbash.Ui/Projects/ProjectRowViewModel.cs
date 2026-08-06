using Kitbash.Core.Projects;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Projects;

/// <summary>
/// One row. Four facts in four positions: what it is called, where it is, what state it
/// is in and when it was last opened.
/// </summary>
public sealed class ProjectRowViewModel
{
    /// <summary>
    /// The tile tones, in the order a list walks them. The tile is a place for the eye to
    /// aim rather than a status, so an app that names no tier gets one from the path.
    /// </summary>
    private static readonly BadgeTier[] Tiles =
    [
        BadgeTier.Accent,
        BadgeTier.Ok,
        BadgeTier.Graph,
        BadgeTier.Modified,
        BadgeTier.Neutral,
    ];

    public ProjectRowViewModel(RecentProject project, ProjectFacts facts, string time)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(facts);

        Project = project;
        Time = time;

        Mark = string.IsNullOrWhiteSpace(facts.Mark) ? MarkOf(project.Name) : facts.Mark;
        MarkTier = facts.MarkTier ?? TileOf(project.Path);

        Chip = facts.Chip;
        ChipTier = facts.ChipTier;
        ChipGlyph = facts.ChipGlyph ?? IconGlyph.AlertCircle;
        HasChipGlyph = facts.ChipGlyph is not null;

        // A path that is not there is broken whatever the app said, since nothing can be
        // opened from it.
        IsBroken = facts.IsBroken || project.IsMissing;
    }

    public RecentProject Project { get; }

    public string Name => Project.Name;

    public string Path => Project.Path;

    public string Time { get; }

    public string Mark { get; }

    public BadgeTier MarkTier { get; }

    public string Chip { get; }

    public bool HasChip => !string.IsNullOrWhiteSpace(Chip);

    public BadgeTier ChipTier { get; }

    public IconGlyph ChipGlyph { get; }

    public bool HasChipGlyph { get; }

    /// <summary>
    /// A broken row keeps its full name and its chip. Only the path drops a tier, since
    /// that is the part that is wrong.
    /// </summary>
    public bool IsBroken { get; }

    /// <summary>
    /// What the first menu item says. A row that cannot be opened offers the only thing
    /// that would help instead.
    /// </summary>
    public string OpenLabel => IsBroken ? "Locate the folder" : "Open";

    /// <summary>Named after the row, so a click on the wrong one is visible first.</summary>
    public string RemoveLabel => $"Remove {Name} from the list";

    public bool Matches(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Path.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>The first letter, upper case. Blank for a name with no letters in it.</summary>
    private static string MarkOf(string name)
    {
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                return char.ToUpper(character, System.Globalization.CultureInfo.CurrentCulture)
                    .ToString();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// A tone that does not move between runs. String.GetHashCode is seeded per process,
    /// so a row would change colour every launch if it were used here.
    /// </summary>
    private static BadgeTier TileOf(string path)
    {
        var total = 0;

        foreach (var character in path)
        {
            total = unchecked((total * 31) + character);
        }

        return Tiles[(int)((uint)total % Tiles.Length)];
    }
}
