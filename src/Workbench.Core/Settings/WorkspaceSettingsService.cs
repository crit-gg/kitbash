using Workbench.Core.IO;

namespace Workbench.Core.Settings;

/// <summary>
/// Reads and writes a workspace's settings. Loaded scopes are cached until a write or
/// a call to <see cref="Reload"/>.
/// </summary>
/// <remarks>
/// Synchronous because settings files are small and callers need them before they can
/// start. Nothing watches the filesystem, so a write in one process is not seen by
/// another until that process reloads.
/// </remarks>
internal sealed class WorkspaceSettingsService : ISettingsService
{
    private readonly WorkspacePaths _paths;
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;
    private readonly IFileSystem _fileSystem;

    private readonly Dictionary<SettingsScope, ISettings> _cache = [];
    private readonly Lock _gate = new();

    public WorkspaceSettingsService(
        WorkspacePaths paths,
        ISettingsDocumentStore store,
        ISettingsValueConverter converter,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _paths = paths;
        _store = store;
        _converter = converter;
        _fileSystem = fileSystem;
    }

    public ISettings Global => ForScope(SettingsScope.Global);

    public ISettings ForTool(string toolId) => ForScope(SettingsScope.ForTool(toolId));

    public void Set<T>(SettingsScope scope, SettingsLayer layer, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var path = _paths.FileFor(scope, layer);

            // Read again so a change made outside this process is not reverted.
            var document = _store.Read(path);
            document.SetValue(key, value);
            _store.Write(path, document);

            if (layer == SettingsLayer.User)
            {
                EnsureUserLayerIgnored();
            }

            _cache.Remove(scope);
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }

    private ISettings ForScope(SettingsScope scope)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(scope, out var cached))
            {
                return cached;
            }

            // Highest precedence first.
            var settings = new LayeredSettings(
                [
                    _store.Read(_paths.FileFor(scope, SettingsLayer.User)),
                    _store.Read(_paths.FileFor(scope, SettingsLayer.TeamShared)),
                ],
                _converter);

            _cache[scope] = settings;
            return settings;
        }
    }

    /// <summary>
    /// The user layer sits inside <c>.workbench</c>, so the folder carries its own
    /// ignore rule instead of relying on the repository's.
    /// </summary>
    private void EnsureUserLayerIgnored()
    {
        var path = Path.Combine(_paths.WorkbenchDirectory, ".gitignore");

        if (_fileSystem.FileExists(path))
        {
            return;
        }

        _fileSystem.CreateDirectory(_paths.WorkbenchDirectory);
        _fileSystem.WriteAllText(
            path,
            $"# Personal Workbench settings. Not shared.{Environment.NewLine}user/{Environment.NewLine}");
    }
}
