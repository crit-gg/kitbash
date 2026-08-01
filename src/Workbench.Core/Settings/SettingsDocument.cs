using System.Collections;
using System.Globalization;

namespace Workbench.Core.Settings;

/// <summary>
/// One settings file as nested tables. Holds no format specific types, so the store
/// decides how it is read and written.
/// </summary>
internal sealed class SettingsDocument
{
    public SettingsDocument()
        : this(new Dictionary<string, object?>(StringComparer.Ordinal))
    {
    }

    public SettingsDocument(Dictionary<string, object?> root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    public Dictionary<string, object?> Root { get; }

    /// <summary>Reads a dotted key from nested tables.</summary>
    public bool TryGetValue(string key, out object? value)
    {
        value = null;

        var segments = Split(key);

        if (segments.Length == 0)
        {
            return false;
        }

        var table = Root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!table.TryGetValue(segments[index], out var child)
                || child is not Dictionary<string, object?> childTable)
            {
                return false;
            }

            table = childTable;
        }

        return table.TryGetValue(segments[^1], out value);
    }

    /// <summary>Writes a dotted key, creating tables as needed.</summary>
    public void SetValue(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var segments = Split(key);

        if (segments.Length == 0)
        {
            throw new ArgumentException("Key must contain at least one segment.", nameof(key));
        }

        var table = Root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            Dictionary<string, object?> childTable;

            if (table.TryGetValue(segments[index], out var child)
                && child is Dictionary<string, object?> existing)
            {
                childTable = existing;
            }
            else
            {
                // A value where a table belongs is replaced.
                childTable = new Dictionary<string, object?>(StringComparer.Ordinal);
                table[segments[index]] = childTable;
            }

            table = childTable;
        }

        table[segments[^1]] = Canonicalize(value);
    }

    private static string[] Split(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Narrows CLR values to the set a document can hold: string, bool, long, double,
    /// arrays of those, and nested tables.
    /// </summary>
    private static object Canonicalize(object value)
    {
        switch (value)
        {
            case string or bool or long or double:
                return value;

            case Enum enumValue:
                return enumValue.ToString();

            case sbyte or byte or short or ushort or int or uint:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);

            case float:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);

            case IEnumerable sequence:
            {
                var items = new List<object?>();

                foreach (var item in sequence)
                {
                    items.Add(item is null ? null : Canonicalize(item));
                }

                return items.ToArray();
            }

            default:
                return value;
        }
    }
}
