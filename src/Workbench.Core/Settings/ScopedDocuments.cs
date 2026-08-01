namespace Workbench.Core.Settings;

/// <summary>
/// One document per scope, read on demand and held until reloaded. Backs both
/// application settings and application state, which differ only in where their
/// files live.
/// </summary>
internal sealed class ScopedDocuments
{
    private readonly Func<SettingsScope, string> _fileFor;
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;

    private readonly Dictionary<SettingsScope, ISettings> _cache = [];
    private readonly Lock _gate = new();

    public ScopedDocuments(
        Func<SettingsScope, string> fileFor,
        ISettingsDocumentStore store,
        ISettingsValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(fileFor);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(converter);

        _fileFor = fileFor;
        _store = store;
        _converter = converter;
    }

    public ISettings For(SettingsScope scope)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(scope, out var cached))
            {
                return cached;
            }

            var values = new LayeredSettings([_store.Read(_fileFor(scope))], _converter);
            _cache[scope] = values;
            return values;
        }
    }

    public void Set<T>(SettingsScope scope, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var path = _fileFor(scope);

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
}
