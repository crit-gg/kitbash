namespace Kitbash.Ui.Controls;

/// <summary>How a wire gets from one pin to the other.</summary>
public enum WireStyle
{
    /// <summary>A cubic that leaves each pin horizontally.</summary>
    Bezier,

    /// <summary>Out, across at the midpoint, then in.</summary>
    Orthogonal,
}
