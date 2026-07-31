namespace Workbench.Core.Settings;

/// <summary>
/// One scope's layers merged. Lookups stop at the first layer defining the key, so
/// merging is per key rather than per file.
/// </summary>
internal sealed class LayeredSettings : ISettings
{
    private readonly IReadOnlyList<SettingsDocument> _highestPrecedenceFirst;

    public LayeredSettings(IReadOnlyList<SettingsDocument> highestPrecedenceFirst)
    {
        _highestPrecedenceFirst = highestPrecedenceFirst;
    }

    public bool TryGet<T>(string key, out T value)
    {
        foreach (var document in _highestPrecedenceFirst)
        {
            if (document.TryGetValue(key, out var raw)
                && SettingsValueConverter.TryConvert<T>(raw, out var converted))
            {
                value = converted;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public T Get<T>(string key, T fallback) => TryGet<T>(key, out var value) ? value : fallback;

    public bool Contains(string key)
    {
        foreach (var document in _highestPrecedenceFirst)
        {
            if (document.TryGetValue(key, out _))
            {
                return true;
            }
        }

        return false;
    }
}
