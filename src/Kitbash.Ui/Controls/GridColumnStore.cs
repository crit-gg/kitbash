using System.Text.Json;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Ui.Controls;

internal sealed class GridColumnStore : IGridColumnStore
{
    private static readonly JsonSerializerOptions Shape = new() { WriteIndented = true };

    private readonly ApplicationPaths _paths;
    private readonly IFileSystem _files;

    public GridColumnStore(ApplicationPaths paths, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(files);

        _paths = paths;
        _files = files;
    }

    public IReadOnlyList<GridColumnState> Read(SettingsScope scope, string key)
    {
        var file = _paths.ColumnsFileFor(scope, key);

        if (!_files.FileExists(file))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<GridColumnState>>(_files.ReadAllText(file)) ?? [];
        }
        catch (Exception exception) when (exception is IOException
                                              or JsonException
                                              or NotSupportedException
                                              or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Write(SettingsScope scope, string key, IReadOnlyList<GridColumnState> layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var file = _paths.ColumnsFileFor(scope, key);

        try
        {
            // Nothing changed means nothing kept, so a grid put back to its declaration
            // leaves no file behind saying otherwise.
            if (layout.Count == 0)
            {
                if (_files.FileExists(file))
                {
                    _files.DeleteFile(file);
                }

                return;
            }

            _files.CreateDirectory(Path.GetDirectoryName(file)!);
            _files.WriteAllText(file, JsonSerializer.Serialize(layout, Shape));
        }
        catch (Exception exception) when (exception is IOException
                                              or JsonException
                                              or NotSupportedException
                                              or UnauthorizedAccessException)
        {
            // A layout is not somebody's work, so a machine that will not take the write
            // keeps going and starts on the declared columns next time.
        }
    }

    public void Forget(SettingsScope scope, string key)
    {
        var file = _paths.ColumnsFileFor(scope, key);

        try
        {
            if (_files.FileExists(file))
            {
                _files.DeleteFile(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
