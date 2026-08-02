namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Bounds a number. Either end may be left out, so this is also the way to say a
/// minimum alone. The bounds are inclusive, and an editor reads them to bound itself
/// rather than waiting for the value to be refused.
/// </summary>
public sealed class RangeRule<T> : ISettingRule<T>
    where T : struct, IComparable<T>
{
    public RangeRule(T? minimum = null, T? maximum = null)
    {
        if (minimum is null && maximum is null)
        {
            throw new ArgumentException("A range needs a minimum, a maximum, or both.", nameof(minimum));
        }

        if (minimum is { } low && maximum is { } high && low.CompareTo(high) > 0)
        {
            throw new ArgumentException($"Minimum {low} is above maximum {high}.", nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public T? Minimum { get; }

    public T? Maximum { get; }

    public string Summary => (Minimum, Maximum) switch
    {
        ({ } low, { } high) => $"Between {low} and {high}",
        ({ } low, null) => $"{low} or more",
        (null, { } high) => $"{high} or less",
        _ => string.Empty,
    };

    public bool Allows(T value, out string? reason)
    {
        if (Minimum is { } low && value.CompareTo(low) < 0)
        {
            reason = $"{value} is below the smallest allowed value, {low}.";
            return false;
        }

        if (Maximum is { } high && value.CompareTo(high) > 0)
        {
            reason = $"{value} is above the largest allowed value, {high}.";
            return false;
        }

        reason = null;
        return true;
    }
}
