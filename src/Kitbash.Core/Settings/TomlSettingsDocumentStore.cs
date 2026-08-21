using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

internal sealed class TomlSettingsDocumentStore : ISettingsDocumentStore
{
    /// <summary>Matches what File.WriteAllText uses, apart from emitting the mark.</summary>
    private static readonly UTF8Encoding MarkedUtf8 = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>What File.WriteAllText uses, named here since every write goes by stream.</summary>
    private static readonly UTF8Encoding PlainUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;

    public TomlSettingsDocumentStore(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public SettingsDocument Read(string path)
    {
        var file = Open(path, out _, out var failure);

        return file.ParseError is null
            ? file.Document
            : throw new SettingsFileUnreadableException(path, file.ParseError, failure);
    }

    public SettingsFile Open(string path) => Open(path, out _, out _);

    private SettingsFile Open(string path, out string text, out Exception? failure)
    {
        failure = null;
        text = string.Empty;

        if (!_fileSystem.FileExists(path))
        {
            // Normal, and the reason a fresh workspace reads every default.
            return new SettingsFile(path, Exists: false, new SettingsDocument(), ParseError: null);
        }

        try
        {
            text = _fileSystem.ReadAllText(path);
            var table = TomlSerializer.Deserialize<TomlTable>(text);
            return new SettingsFile(path, Exists: true, new SettingsDocument(ToTables(table ?? [])), ParseError: null);
        }
        catch (TomlException exception)
        {
            failure = exception;
            return Unreadable(path, $"'{path}' is not valid TOML. {exception.Message}", willNotParse: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = exception;
            return Unreadable(path, $"'{path}' could not be read. {exception.Message}", willNotParse: false);
        }
    }

    /// <summary>
    /// The document is empty and the error says why, which is the pair that makes this
    /// file unwritable. After the fact an empty document and a broken one look the same.
    /// </summary>
    private static SettingsFile Unreadable(string path, string error, bool willNotParse) =>
        new(path, Exists: true, new SettingsDocument(), error) { WillNotParse = willNotParse };

    /// <summary>
    /// Rewrites the file from the model, so comments, blank lines and key order all go.
    /// This is for a file the app generates and nobody edits. Anything a person may have
    /// written in goes through <see cref="Apply"/>, which keeps all three.
    /// </summary>
    public void Write(string path, SettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Save(path, TomlSerializer.Serialize(ToToml(document.Root)));
    }

    public void Apply(string path, IReadOnlyList<SettingsEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        // Whether the file can be used is the verdict Open publishes, which is the one the
        // settings window draws. A second opinion here would let a write land on a file
        // the window has already called unreadable.
        var file = Open(path, out var text, out _);

        if (file.ParseError is { } error)
        {
            throw new SettingsFileUnreadableException(path, error);
        }

        var document = new TomlDocument(text);

        if (document.HasErrors)
        {
            throw new SettingsFileUnreadableException(path, $"'{path}' is not valid TOML.");
        }

        var changed = false;

        foreach (var edit in edits)
        {
            changed |= edit.IsRemoval
                ? document.RemoveValue(edit.Key)
                : document.SetValue(edit.Key, edit.Value!);
        }

        // A write costs the file nothing now, so this is only about leaving a file, and
        // its timestamp, alone rather than writing back what it already said.
        if (changed)
        {
            Save(path, document.ToString(), file.Exists && StartsWithByteOrderMark(path));
        }
    }

    private void Save(string path, string text, bool byteOrderMark = false)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        // Move into place so an interrupted write cannot truncate the file.
        var temporary = path + ".tmp";

        using (var stream = _fileSystem.Create(temporary))
        {
            using (var writer = new StreamWriter(stream, byteOrderMark ? MarkedUtf8 : PlainUtf8, -1, leaveOpen: true))
            {
                writer.Write(text);
            }

            // A rename is journalled and the content it moves is not, so without this a
            // machine losing power soon after can leave a file of the right length full
            // of zero bytes, which is a settings file nothing can parse.
            Flush(stream);
        }

        _fileSystem.MoveFile(temporary, path, overwrite: true);
    }

    /// <summary>Pushes the content past the operating system's own cache.</summary>
    private static void Flush(Stream stream)
    {
        if (stream is FileStream file)
        {
            file.Flush(flushToDisk: true);

            return;
        }

        stream.Flush();
    }

    /// <summary>
    /// Whether the file opens with a UTF-8 byte order mark.
    /// </summary>
    private bool StartsWithByteOrderMark(string path)
    {
        try
        {
            using var stream = _fileSystem.OpenRead(path);
            Span<byte> head = stackalloc byte[3];

            return stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length
                && head is [0xEF, 0xBB, 0xBF];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The read that matters already succeeded, so this cannot be a reason to fail.
            return false;
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

    // Tomlyn has two array types and a table array is not a TomlArray, so [[a.b]] needs
    // its own case or it reaches the document as a Tomlyn type. Reading only: writing one
    // back through ToTomlValue would make a plain array of inline tables.
    private static object? FromToml(object? value) => value switch
    {
        TomlTable table => ToTables(table),
        TomlTableArray tables => tables.Select(FromToml).ToArray(),
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
