using System.Globalization;

namespace Kitbash.Tools;

/// <summary>
/// One answer a script asks for before it runs. The form draws it and its value goes on
/// the command line.
/// </summary>
/// <param name="Key">Names the value in the form and in what is remembered.</param>
/// <param name="Argument">
/// The flag the value goes behind, or null for a plain argument in declaration order. One
/// ending in <c>=</c> is joined to the value instead of being handed over separately.
/// </param>
/// <param name="Remember">The value is kept for this person on this machine and comes back next time.</param>
/// <param name="Default">Held as text whatever the kind is, since a command line is text.</param>
public sealed record ToolInput(
    string Key,
    string Name,
    string Description,
    ToolInputKind Kind,
    string? Argument,
    bool Required,
    bool Remember,
    string Default,
    IReadOnlyList<ToolInputChoice> Choices,
    double? Minimum,
    double? Maximum)
{
    /// <summary>What a default expands to before the form opens.</summary>
    public const string WorkspaceToken = "{workspace}";

    /// <summary>What a checked box hands over when nothing names a flag.</summary>
    public const string True = "true";

    /// <summary>Why this value is not usable, or null when it is.</summary>
    public string? Check(string? value)
    {
        var text = value?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return Required && Kind != ToolInputKind.Boolean ? $"{Name} is needed." : null;
        }

        return Kind switch
        {
            ToolInputKind.Number or ToolInputKind.Integer => CheckNumber(text),
            ToolInputKind.Choice => Choices.Any(choice => choice.Value == text)
                ? null
                : $"{text} is not one of the options.",
            _ => null,
        };
    }

    /// <summary>
    /// What this input adds to the command line, in order. A value that is not there adds
    /// nothing, and so does a box that is not ticked.
    /// </summary>
    public IReadOnlyList<string> Arguments(string? value)
    {
        var text = value?.Trim() ?? string.Empty;

        if (Kind == ToolInputKind.Boolean)
        {
            var ticked = string.Equals(text, True, StringComparison.OrdinalIgnoreCase);

            if (Argument is null)
            {
                return [ticked ? True : "false"];
            }

            return ticked ? [Argument] : [];
        }

        if (text.Length == 0)
        {
            return [];
        }

        if (Argument is null)
        {
            return [text];
        }

        // A flag written with a trailing equals is one token, which is what a script
        // parsing its own arguments by hand usually expects.
        return Argument.EndsWith('=') ? [Argument + text] : [Argument, text];
    }

    /// <summary>The default with the open workspace put in, or empty when there is none.</summary>
    public string DefaultFor(string? workspaceRoot) =>
        Default.Contains(WorkspaceToken, StringComparison.Ordinal)
            ? Default.Replace(WorkspaceToken, workspaceRoot ?? string.Empty, StringComparison.Ordinal)
            : Default;

    private string? CheckNumber(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return $"{Name} takes a number.";
        }

        if (Kind == ToolInputKind.Integer && number != Math.Truncate(number))
        {
            return $"{Name} takes a whole number.";
        }

        if (Minimum is { } least && number < least)
        {
            return $"{Name} cannot be below {Write(least)}.";
        }

        return Maximum is { } most && number > most ? $"{Name} cannot be above {Write(most)}." : null;
    }

    private static string Write(double number) => number.ToString(CultureInfo.InvariantCulture);
}
