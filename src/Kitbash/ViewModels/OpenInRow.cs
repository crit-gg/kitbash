namespace Kitbash.ViewModels;

/// <summary>
/// One row of the Open in menu, already decided. The view turns this into controls and
/// chooses nothing. The menu is one flat list, so a row has no children.
/// </summary>
public sealed record OpenInRow
{
    /// <summary>The rule between two groups.</summary>
    public static OpenInRow Separator { get; } = new() { IsSeparator = true };

    public string Header { get; init; } = string.Empty;

    public bool IsSeparator { get; init; }

    /// <summary>Names the brand mark, or null for a row that carries none.</summary>
    public string? IconKey { get; init; }

    /// <summary>What pressing it does. Null on a separator.</summary>
    public Action? Invoke { get; init; }
}
