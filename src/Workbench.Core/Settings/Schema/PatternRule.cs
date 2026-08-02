using System.Text.RegularExpressions;

namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The string has to match a pattern. The summary is given rather than derived, because
/// a regex is not a message and nobody reading one learns what the setting wants.
/// </summary>
public sealed class PatternRule : ISettingRule<string>
{
    // The pattern comes from a schema and the input comes from a file a person edited,
    // so a bad pair can backtrack for a long time. A refusal beats a hung window.
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);

    private readonly Regex _pattern;

    /// <param name="pattern">Matched anywhere in the value unless it is anchored.</param>
    /// <param name="summary">What the value has to be, in plain language.</param>
    /// <param name="ignoreCase">
    /// Compared without a culture, so a value means the same thing on every machine.
    /// </param>
    public PatternRule(string pattern, string summary, bool ignoreCase = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        var options = RegexOptions.CultureInvariant;

        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        _pattern = new Regex(pattern, options, Limit);
        Summary = summary;
    }

    public string Summary { get; }

    public string Pattern => _pattern.ToString();

    public bool Allows(string value, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            if (_pattern.IsMatch(value))
            {
                reason = null;
                return true;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Took too long to decide, so it is refused and the layer below is used.
        }

        reason = $"'{value}' is not allowed here. {Summary}.";
        return false;
    }
}
