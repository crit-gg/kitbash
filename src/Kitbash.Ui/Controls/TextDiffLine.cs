namespace Kitbash.Ui.Controls;

/// <summary>
/// What one line of a diff is. The first three are what every diff has. The rest are what a
/// merge has, where a line came from one side, the other, or a decision somebody made.
/// </summary>
public enum TextDiffLineKind
{
    /// <summary>Present on both sides, and the only kind with a number down each gutter.</summary>
    Context,

    Added,
    Removed,

    /// <summary>From the branch being merged into.</summary>
    Ours,

    /// <summary>From the branch being merged in.</summary>
    Theirs,

    /// <summary>What a person picked where the two sides disagreed.</summary>
    Chosen,

    /// <summary>Settled without asking, so it is drawn quietly.</summary>
    Settled,

    /// <summary>The heading over a run of lines, which is not a line of the file.</summary>
    Heading,
}

/// <summary>
/// A run of characters inside a line, as a start and a length in characters.
/// </summary>
/// <param name="Start">Counted from zero.</param>
public readonly record struct TextDiffSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool IsEmpty => Length <= 0;
}

/// <summary>
/// One line of a diff, ready to draw.
/// </summary>
/// <param name="Text">The line without its marker and without its newline.</param>
/// <param name="OldNumber">Its number on the old side, or null when it is not on that side.</param>
/// <param name="NewNumber">Its number on the new side, or null when it is not on that side.</param>
/// <param name="Changed">
/// The runs inside <paramref name="Text"/> that actually differ from the line this one is
/// paired with. Empty when nothing paired it or when the whole line is new.
/// </param>
public sealed record TextDiffLine(
    TextDiffLineKind Kind,
    string Text,
    int? OldNumber = null,
    int? NewNumber = null,
    IReadOnlyList<TextDiffSpan>? Changed = null)
{
    /// <summary>The runs that differ, never null.</summary>
    public IReadOnlyList<TextDiffSpan> Changed { get; init; } = Changed ?? [];

    /// <summary>The one character in front of the line, or empty where there is none.</summary>
    public string Symbol => Kind switch
    {
        TextDiffLineKind.Added => "+",
        TextDiffLineKind.Removed => "-",
        TextDiffLineKind.Ours => "Y",
        TextDiffLineKind.Theirs => "M",
        TextDiffLineKind.Chosen => "D",
        _ => "",
    };

    /// <summary>
    /// The number to draw, which is the new side where a line has one and the old side
    /// otherwise, so a removed line still says where it was.
    /// </summary>
    public int? Number => NewNumber ?? OldNumber;
}
