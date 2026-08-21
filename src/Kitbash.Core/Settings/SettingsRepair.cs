using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

internal sealed class SettingsRepair : ISettingsRepair
{
    /// <summary>Added to the file's own name. A previous one is written over.</summary>
    private const string BrokenSuffix = ".broken";

    private readonly ISettingsDocumentStore _store;
    private readonly IFileSystem _fileSystem;

    public SettingsRepair(ISettingsDocumentStore store, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _store = store;
        _fileSystem = fileSystem;
    }

    public string? Replace(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Only the content being bad counts. A file that would not open may be a good one
        // behind a lock, and replacing that would throw away settings nothing was wrong with.
        if (!_store.Open(path).WillNotParse)
        {
            return null;
        }

        var broken = path + BrokenSuffix;

        try
        {
            // Moved rather than deleted, so what a person had is still there to read.
            _fileSystem.MoveFile(path, broken, overwrite: true);

            return broken;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file stays where it is and reads as empty. Every reader of it takes a
            // default, so this costs the settings rather than the start.
            return null;
        }
    }
}
