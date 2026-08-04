using System.Text.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Ui.Docking;

internal sealed class DockLayoutStore : IDockLayoutStore
{
    private readonly ApplicationPaths _paths;
    private readonly IFileSystem _files;
    private readonly IDockSerializer _serializer;

    public DockLayoutStore(ApplicationPaths paths, IFileSystem files, IDockSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(serializer);

        _paths = paths;
        _files = files;
        _serializer = serializer;
    }

    public IRootDock? Read(SettingsScope scope, string view)
    {
        var file = _paths.LayoutFileFor(scope, view);

        if (!_files.FileExists(file))
        {
            return null;
        }

        try
        {
            return _serializer.Deserialize<IRootDock?>(_files.ReadAllText(file));
        }
        catch (Exception exception) when (exception is IOException
                                              or JsonException
                                              or NotSupportedException
                                              or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(SettingsScope scope, string view, IRootDock layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var file = _paths.LayoutFileFor(scope, view);

        try
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory)
            {
                _files.CreateDirectory(directory);
            }

            _files.WriteAllText(file, _serializer.Serialize(layout));
        }
        catch (Exception exception) when (exception is IOException
                                              or JsonException
                                              or NotSupportedException
                                              or UnauthorizedAccessException)
        {
            // Survived rather than reported, the way a workspace that cannot be scaffolded
            // is. The next run opens on the default layout, which is not a loss of work.
        }
    }

    public void Forget(SettingsScope scope, string view)
    {
        try
        {
            _files.DeleteFile(_paths.LayoutFileFor(scope, view));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
