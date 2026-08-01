namespace Workbench.Core.Settings;

internal sealed class ApplicationSettings : IApplicationSettings
{
    private readonly ApplicationPaths _paths;
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;

    private readonly Dictionary<SettingsScope, ISettings> _cache = [];
    private readonly Lock _gate = new();

    public ApplicationSettings(
        ApplicationPaths paths,
        ISettingsDocumentStore store,
        ISettingsValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(converter);

        _paths = paths;
        _store = store;
        _converter = converter;
    }

    public ISettings Global => ForScope(SettingsScope.Global);

    public ISettings ForTool(string toolId) => ForScope(SettingsScope.ForTool(toolId));

    public void Set<T>(SettingsScope scope, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var path = _paths.FileFor(scope);

            // Read again so a change made outside this process is not reverted.
            var document = _store.Read(path);
            document.SetValue(key, value);
            _store.Write(path, document);

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

            var settings = new LayeredSettings([_store.Read(_paths.FileFor(scope))], _converter);
            _cache[scope] = settings;
            return settings;
        }
    }
}
