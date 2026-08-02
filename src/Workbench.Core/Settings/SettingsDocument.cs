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

    /// <summary>
    /// Drops a dotted key, and any table it leaves empty. Returns whether anything was
    /// there. This is what resetting a setting does, rather than writing today's default
    /// into the file, which would pin that value and opt the person out of every future
    /// change to it.
    /// </summary>
    public bool RemoveValue(string key)
    {
        var segments = Split(key);

        if (segments.Length == 0)
        {
            return false;
        }

        // The tables walked through, so an emptied one can be dropped on the way back.
        var trail = new Dictionary<string, object?>[segments.Length];
        var table = Root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            trail[index] = table;

            if (!table.TryGetValue(segments[index], out var child)
                || child is not Dictionary<string, object?> childTable)
            {
                return false;
            }

            table = childTable;
        }

        trail[^1] = table;

        if (!table.Remove(segments[^1]))
        {
            return false;
        }

        for (var index = segments.Length - 2; index >= 0; index--)
        {
            if (trail[index + 1].Count > 0)
            {
                break;
            }

            trail[index].Remove(segments[index]);
        }

        return true;
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
