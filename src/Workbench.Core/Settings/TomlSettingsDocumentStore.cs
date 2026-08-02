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
        var file = Open(path, out var failure);

        return file.ParseError is null
            ? file.Document
            : throw new SettingsFileUnreadableException(path, file.ParseError, failure);
    }

    public SettingsFile Open(string path) => Open(path, out _);

    private SettingsFile Open(string path, out Exception? failure)
    {
        failure = null;

        if (!_fileSystem.FileExists(path))
        {
            // Normal, and the reason a fresh workspace reads every default.
            return new SettingsFile(path, Exists: false, new SettingsDocument(), ParseError: null);
        }

        try
        {
            var table = TomlSerializer.Deserialize<TomlTable>(_fileSystem.ReadAllText(path));
            return new SettingsFile(path, Exists: true, new SettingsDocument(ToTables(table ?? [])), ParseError: null);
        }
        catch (TomlException exception)
        {
            failure = exception;
            return Unreadable(path, $"'{path}' is not valid TOML. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = exception;
            return Unreadable(path, $"'{path}' could not be read. {exception.Message}");
        }
    }

    /// <summary>
    /// The document is empty and the error says why, which is the pair that makes this
    /// file unwritable. After the fact an empty document and a broken one look the same.
    /// </summary>
    private static SettingsFile Unreadable(string path, string error) =>
        new(path, Exists: true, new SettingsDocument(), error);

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

    public void Apply(string path, IReadOnlyList<SettingsEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        var file = Open(path);

        if (file.ParseError is { } error)
        {
            throw new SettingsFileUnreadableException(path, error);
        }

        var changed = false;

        foreach (var edit in edits)
        {
            if (edit.IsRemoval)
            {
                changed |= file.Document.RemoveValue(edit.Key);
            }
            else
            {
                file.Document.SetValue(edit.Key, edit.Value!);
                changed = true;
            }
        }

        // Removing a key that was never there changes nothing, and a write would cost
        // the file its comments for no reason.
        if (changed)
        {
            Write(path, file.Document);
        }
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
