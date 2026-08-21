namespace Kitbash.Core.Settings;

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

            // Open rather than Read, so a file that will not parse reads as empty here
            // instead of throwing at whoever asked for a setting. Writing still refuses it.
            var values = new LayeredSettings([_store.Open(_fileFor(scope)).Document], _converter);
            _cache[scope] = values;
            return values;
        }
    }

    public void Set<T>(SettingsScope scope, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Apply(scope, [SettingsEdit.Set(key, value)]);
    }

    public void Apply(SettingsScope scope, IReadOnlyList<SettingsEdit> edits)
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
            _store.Apply(_fileFor(scope), edits);

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
