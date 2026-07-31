using System.Collections;
using System.Globalization;
using Tomlyn;
using Tomlyn.Model;

namespace Workbench.Core.Settings;

/// <summary>The only type that knows settings are stored as TOML.</summary>
internal sealed class SettingsDocument
{
    private SettingsDocument(TomlTable root)
    {
        _root = root;
    }

    private readonly TomlTable _root;

    /// <summary>A missing file loads as empty.</summary>
    public static SettingsDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            return new SettingsDocument(new TomlTable());
        }

        try
        {
            var model = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path));
            return new SettingsDocument(model ?? new TomlTable());
        }
        catch (TomlException exception)
        {
            throw new InvalidDataException($"'{path}' is not valid TOML. {exception.Message}", exception);
        }
    }

    /// <summary>Reads a dotted key from nested tables.</summary>
    public bool TryGetValue(string key, out object? value)
    {
        value = null;

        var segments = Split(key);

        if (segments.Length == 0)
        {
            return false;
        }

        var table = _root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!table.TryGetValue(segments[index], out var child) || child is not TomlTable childTable)
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
        var segments = Split(key);

        if (segments.Length == 0)
        {
            throw new ArgumentException("Key must contain at least one segment.", nameof(key));
        }

        var table = _root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            TomlTable childTable;

            if (table.TryGetValue(segments[index], out var child) && child is TomlTable existing)
            {
                childTable = existing;
            }
            else
            {
                // A scalar where a table belongs is replaced.
                childTable = new TomlTable();
                table[segments[index]] = childTable;
            }

            table = childTable;
        }

        table[segments[^1]] = Normalize(value);
    }

    /// <summary>
    /// Rewrites the file from the model, so comments and formatting are lost. Tomlyn
    /// keeps trivia on its syntax tree, so this can be made lossless later without
    /// changing callers.
    /// </summary>
    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Move into place so an interrupted save cannot truncate the file.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, TomlSerializer.Serialize(_root));
        File.Move(temporary, path, overwrite: true);
    }

    private static string[] Split(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Maps CLR values onto the types TOML can store.</summary>
    private static object Normalize(object value)
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
                var array = new TomlArray();

                foreach (var item in sequence)
                {
                    array.Add(Normalize(item));
                }

                return array;
            }

            default:
                return value;
        }
    }
}
