using Kitbash.Core.IO;

namespace Kitbash.Core.Settings;

/// <summary>
/// Reads and writes a workspace's settings. Loaded scopes are cached until a write or
/// a call to <see cref="Reload"/>.
/// </summary>
internal sealed class WorkspaceSettingsService : ISettingsService
{
    private readonly WorkspacePaths _paths;
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;
    private readonly IFileSystem _fileSystem;

    private readonly Dictionary<SettingsScope, ISettings> _cache = [];
    private readonly Dictionary<(SettingsScope Scope, SettingsLayer Layer), ISettings> _layers = [];
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

    public ISettings In(SettingsScope scope, SettingsLayer layer)
    {
        lock (_gate)
        {
            if (_layers.TryGetValue((scope, layer), out var cached))
            {
                return cached;
            }

            var settings = new LayeredSettings([_store.Read(_paths.FileFor(scope, layer))], _converter);

            _layers[(scope, layer)] = settings;
            return settings;
        }
    }

    public void Set<T>(SettingsScope scope, SettingsLayer layer, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Apply(scope, layer, [SettingsEdit.Set(key, value)]);
    }

    public void Apply(SettingsScope scope, SettingsLayer layer, IReadOnlyList<SettingsEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            // The store reads the file again first, so a change made outside this
            // process is not reverted, and refuses one it could not read.
            _store.Apply(_paths.FileFor(scope, layer), edits);

            if (layer == SettingsLayer.User)
            {
                EnsureUserLayerIgnored();
            }

            _cache.Remove(scope);
            _layers.Remove((scope, layer));
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _cache.Clear();
            _layers.Clear();
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
    /// The user layer sits inside <c>.kitbash</c>, so the folder carries its own
    /// ignore rule instead of relying on the repository's.
    /// </summary>
    private void EnsureUserLayerIgnored()
    {
        var path = Path.Combine(_paths.KitbashDirectory, ".gitignore");

        if (_fileSystem.FileExists(path))
        {
            return;
        }

        _fileSystem.CreateDirectory(_paths.KitbashDirectory);
        _fileSystem.WriteAllText(
            path,
            $"# Personal Kitbash settings. Not shared.{Environment.NewLine}user/{Environment.NewLine}");
    }
}
