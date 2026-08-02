namespace Workbench.Core.Settings.Schema;

/// <summary>
/// How long a string may be, counted in characters. Either end may be left out.
/// </summary>
/// <remarks>
/// The count is <see cref="string.Length"/>, which is UTF-16 units rather than what a
/// person would call a character. An emoji counts as two. That is what every text box
/// counts as well, so the two agree, and no bound here is close enough for the
/// difference to matter.
/// </remarks>
public sealed class LengthRule : ISettingRule<string>
{
    public LengthRule(int? minimum = null, int? maximum = null)
    {
        if (minimum is null && maximum is null)
        {
            throw new ArgumentException("A length needs a minimum, a maximum, or both.", nameof(minimum));
        }

        if (minimum is { } low)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(low, nameof(minimum));
        }

        if (maximum is { } high)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(high, nameof(maximum));
        }

        if (minimum > maximum)
        {
            throw new ArgumentException($"Minimum {minimum} is above maximum {maximum}.", nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public int? Minimum { get; }

    public int? Maximum { get; }

    public string Summary => (Minimum, Maximum) switch
    {
        ({ } low, { } high) => $"Between {low} and {Characters(high)}",
        ({ } low, null) => $"{Characters(low)} or more",
        (null, { } high) => $"{Characters(high)} or fewer",
        _ => string.Empty,
    };

    public bool Allows(string value, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Minimum is { } low && value.Length < low)
        {
            reason = $"This is {Characters(value.Length)} and needs at least {low}.";
            return false;
        }

        if (Maximum is { } high && value.Length > high)
        {
            reason = $"This is {Characters(value.Length)} and cannot be longer than {high}.";
            return false;
        }

        reason = null;
        return true;
    }

    // Core stays on Tomlyn alone, so the one plural here is written out rather than
    // pulling Humanizer into the contract every tool references.
    private static string Characters(int count) => count == 1 ? "1 character" : $"{count} characters";
}
