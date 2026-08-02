namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The value has to be one of a set the schema knows in full. This is the closed
/// choice, so a value outside the set is refused and the layer below decides instead.
/// </summary>
/// <remarks>
/// Not the same thing as <see cref="ISettingChoices{T}"/>, and the difference matters.
/// A theme name is closed, so an unknown one is wrong. An engine version is a list of
/// what is installed, so a pinned version that is missing is still the right value and
/// must be kept. This rule is also where an editor gets its options from, since a closed
/// set is known without asking anything.
/// </remarks>
public sealed class ChoiceRule<T> : ISettingRule<T>
    where T : notnull
{
    private readonly IEqualityComparer<T> _comparer;

    /// <param name="options">At least one, and no two may share a value.</param>
    /// <param name="comparer">
    /// How a stored value is matched to an option. Strings compare exactly by default,
    /// so pass <see cref="StringComparer.OrdinalIgnoreCase"/> where case is not meant to
    /// matter. An enum needs nothing, since it is parsed by name before it gets here.
    /// </param>
    public ChoiceRule(IReadOnlyList<SettingChoice<T>> options, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            throw new ArgumentException("A choice needs at least one option.", nameof(options));
        }

        _comparer = comparer ?? EqualityComparer<T>.Default;

        var seen = new HashSet<T>(_comparer);

        foreach (var option in options)
        {
            ArgumentNullException.ThrowIfNull(option);

            if (!seen.Add(option.Value))
            {
                throw new ArgumentException($"Option '{option.Value}' appears twice.", nameof(options));
            }
        }

        Options = options;
    }

    public IReadOnlyList<SettingChoice<T>> Options { get; }

    public string Summary => $"One of {Join(Options.Select(option => option.Label))}";

    public bool Allows(T value, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(value);

        foreach (var option in Options)
        {
            if (_comparer.Equals(option.Value, value))
            {
                reason = null;
                return true;
            }
        }

        reason = $"'{value}' is not one of {Join(Options.Select(option => option.Label))}.";
        return false;
    }

    private static string Join(IEnumerable<string> parts)
    {
        var labels = parts.ToArray();

        return labels.Length switch
        {
            0 => string.Empty,
            1 => labels[0],
            _ => $"{string.Join(", ", labels[..^1])} or {labels[^1]}",
        };
    }
}
