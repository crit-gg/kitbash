using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>A run of one colour inside a line.</summary>
public readonly record struct TextDiffColour(TextDiffSpan Span, IBrush Brush);

/// <summary>
/// Colours the words of a line by what they mean in the language they are written in. The
/// library ships none, so a diff is coloured by line kind alone until something supplies one.
/// </summary>
public interface ITextDiffColouring
{
    /// <summary>
    /// The coloured runs inside one line, or nothing when the file is not one this knows.
    /// Called once per line as it is realised, so it has to be cheap.
    /// </summary>
    /// <param name="text">The line, with no marker and no newline.</param>
    /// <param name="path">The file the line came from, for picking a grammar.</param>
    IReadOnlyList<TextDiffColour> Colour(string text, string path);
}
