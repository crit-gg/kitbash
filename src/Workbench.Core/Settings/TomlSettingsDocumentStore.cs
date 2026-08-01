using Tomlyn;
using Tomlyn.Model;
using Workbench.Core.IO;

namespace Workbench.Core.Settings;

internal sealed class TomlSettingsDocumentStore : ISettingsDocumentStore
{
    private readonly IFileSystem _fileSystem;

    public TomlSettingsDocumentStore(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public SettingsDocument Read(string path)
    {
        if (!_fileSystem.FileExists(path))
        {
            return new SettingsDocument();
        }

        try
        {
            var table = TomlSerializer.Deserialize<TomlTable>(_fileSystem.ReadAllText(path));
            return new SettingsDocument(ToTables(table ?? []));
        }
        catch (TomlException exception)
        {
            throw new InvalidDataException($"'{path}' is not valid TOML. {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Rewrites the file, so comments and formatting are lost. Tomlyn keeps trivia on
    /// its syntax tree, so this can be made lossless later without changing callers.
    /// </summary>
    public void Write(string path, SettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        // Move into place so an interrupted write cannot truncate the file.
        var temporary = path + ".tmp";
        _fileSystem.WriteAllText(temporary, TomlSerializer.Serialize(ToToml(document.Root)));
        _fileSystem.MoveFile(temporary, path, overwrite: true);
    }

    private static Dictionary<string, object?> ToTables(TomlTable table)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in table)
        {
            result[pair.Key] = FromToml(pair.Value);
        }

        return result;
    }

    private static object? FromToml(object? value) => value switch
    {
        TomlTable table => ToTables(table),
        TomlArray array => array.Select(FromToml).ToArray(),
        _ => value,
    };

    private static TomlTable ToToml(Dictionary<string, object?> values)
    {
        var table = new TomlTable();

        foreach (var pair in values)
        {
            table[pair.Key] = ToTomlValue(pair.Value)!;
        }

        return table;
    }

    private static object? ToTomlValue(object? value)
    {
        switch (value)
        {
            case Dictionary<string, object?> table:
                return ToToml(table);

            case object?[] items:
            {
                var array = new TomlArray();

                foreach (var item in items)
                {
                    array.Add(ToTomlValue(item));
                }

                return array;
            }

            default:
                return value;
        }
    }
}
