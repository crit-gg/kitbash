using System.Collections;

namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The one place a setting is defined. Its key, what it is called, what it means, what
/// it is when nothing says otherwise, and what it is allowed to be.
/// </summary>
/// <remarks>
/// Hold these as instance members reached through a constructor. A
/// <c>public static readonly</c> descriptor is the obvious shortcut and it is ambient
/// state, which is why <see cref="WindowSettings"/> is handed a schema rather than
/// naming one.
/// </remarks>
public sealed class SettingDescriptor<T> : ISettingDescriptor
    where T : notnull
{
    private static readonly HashSet<Type> WholeNumbers =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
    ];

    private static readonly HashSet<Type> FractionalNumbers =
    [
        typeof(float), typeof(double), typeof(decimal),
    ];

    private readonly string _key = string.Empty;
    private readonly string _name = string.Empty;
    private readonly string _description = string.Empty;
    private readonly IReadOnlyList<ISettingRule<T>> _rules = [];
    private readonly SettingEditor _editor = SettingEditor.Derived;

    /// <summary>A dotted key onto nested tables, such as <c>editor.font.size</c>.</summary>
    public required string Key
    {
        get => _key;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _key = value;
        }
    }

    public required string Name
    {
        get => _name;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    /// <summary>
    /// Required, and drawn. Where a setting only takes effect later, that goes here as a
    /// sentence, since there is no field for it.
    /// </summary>
    public required string Description
    {
        get => _description;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _description = value;
        }
    }

    public required T Default { get; init; }

    public IReadOnlyList<ISettingRule<T>> Rules
    {
        get => _rules;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _rules = value;
        }
    }

    /// <summary>Options discovered at runtime. A closed set is a <see cref="ChoiceRule{T}"/> instead.</summary>
    public ISettingChoices<T>? Choices { get; init; }

    public ISettingProbe? Probe { get; init; }

    /// <summary>
    /// Worked out from the type and the options unless one is named here. Setting it is
    /// for the cases the derivation gets wrong, such as a short closed choice that still
    /// reads better as a dropdown.
    /// </summary>
    public SettingEditor Editor
    {
        get => _editor is SettingEditor.Derived ? Derive() : _editor;
        init => _editor = value;
    }

    /// <summary>Drawn after a number, such as px or ms.</summary>
    public string? Unit { get; init; }

    /// <summary>Shown but never written. The value still comes from a file.</summary>
    public bool IsReadOnly { get; init; }

    public Type ValueType => typeof(T);

    /// <summary>The merged value, with this descriptor's own default behind it.</summary>
    public T Read(ISettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Get(Key, Default);
    }

    public SettingCheck Check(object? raw, ISettingsValueConverter converter, out object? value)
    {
        ArgumentNullException.ThrowIfNull(converter);

        value = null;

        if (raw is null)
        {
            return SettingCheck.Absent;
        }

        if (!converter.TryConvert<T>(raw, out var typed))
        {
            return SettingCheck.WrongType($"{Show(raw)} is not {Expected()}.");
        }

        foreach (var rule in Rules)
        {
            if (!rule.Allows(typed, out var reason))
            {
                return SettingCheck.Rejected(reason ?? $"{Show(raw)} is not allowed here.");
            }
        }

        value = typed;
        return SettingCheck.Ok;
    }

    public bool IsDefault(object? value) => value is T typed && Same(typed, Default);

    public async Task<IReadOnlyList<SettingChoice>> Offer(CancellationToken token)
    {
        if (Choices is not null)
        {
            var offered = await Choices.Offer(token).ConfigureAwait(false);
            return [.. offered.Select(Erase)];
        }

        foreach (var rule in Rules)
        {
            if (rule is ChoiceRule<T> closed)
            {
                return [.. closed.Options.Select(Erase)];
            }
        }

        return [];
    }

    object ISettingDescriptor.Default => Default;

    // IReadOnlyList is covariant, so the typed rules are already the untyped ones.
    IReadOnlyList<ISettingRule> ISettingDescriptor.Rules => Rules;

    private static SettingChoice Erase(SettingChoice<T> choice) =>
        new(choice.Value, choice.Label, choice.Description);

    /// <summary>
    /// Arrays are the one type a document holds that does not compare by value, so they
    /// are walked. Everything else is a string, a bool, a number or an enum.
    /// </summary>
    private static bool Same(T left, T right)
    {
        if (left is Array first && right is Array second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (var index = 0; index < first.Length; index++)
            {
                if (!Equals(first.GetValue(index), second.GetValue(index)))
                {
                    return false;
                }
            }

            return true;
        }

        return EqualityComparer<T>.Default.Equals(left, right);
    }

    private static string Show(object raw) => raw switch
    {
        bool value => value ? "'true'" : "'false'",
        string value => $"'{value}'",
        IEnumerable and not string => "A list",
        _ => $"'{raw}'",
    };

    private static string Expected()
    {
        var type = typeof(T);

        if (type == typeof(bool))
        {
            return "true or false";
        }

        if (type == typeof(string))
        {
            return "text";
        }

        if (type.IsEnum)
        {
            return $"one of {string.Join(", ", Enum.GetNames(type))}";
        }

        if (type.IsArray)
        {
            return "a list";
        }

        if (WholeNumbers.Contains(type))
        {
            return "a whole number";
        }

        return FractionalNumbers.Contains(type) ? "a number" : "the right kind of value";
    }

    /// <summary>
    /// A bool is a toggle, a closed choice is a segment when it is short enough to draw
    /// one and a dropdown otherwise, a number is a number, an array is a list, and
    /// anything left is text. The two thresholds below are a guess at what fits, not a
    /// measurement, so name an editor where they read wrong.
    /// </summary>
    private SettingEditor Derive()
    {
        const int MostSegmentOptions = 3;
        const int LongestSegmentLabel = 12;

        var type = typeof(T);

        if (type == typeof(bool))
        {
            return SettingEditor.Toggle;
        }

        foreach (var rule in Rules)
        {
            if (rule is not ChoiceRule<T> closed)
            {
                continue;
            }

            return closed.Options.Count <= MostSegmentOptions
                && closed.Options.All(option => option.Label.Length <= LongestSegmentLabel)
                    ? SettingEditor.Segment
                    : SettingEditor.Select;
        }

        if (Choices is not null || type.IsEnum)
        {
            return SettingEditor.Select;
        }

        if (type.IsArray)
        {
            return SettingEditor.List;
        }

        return WholeNumbers.Contains(type) || FractionalNumbers.Contains(type)
            ? SettingEditor.Number
            : SettingEditor.Text;
    }
}
