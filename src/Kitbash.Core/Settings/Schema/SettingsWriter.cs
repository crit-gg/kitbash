namespace Kitbash.Core.Settings.Schema;

internal sealed class SettingsWriter : ISettingsWriter
{
    private readonly IReadOnlyDictionary<SettingsHome, ISettingsHome> _homes;

    public SettingsWriter(IEnumerable<ISettingsHome> homes)
    {
        ArgumentNullException.ThrowIfNull(homes);

        var byHome = new Dictionary<SettingsHome, ISettingsHome>();

        foreach (var home in homes)
        {
            byHome[home.Home] = home;
        }

        _homes = byHome;
    }

    public void Write(
        SettingsScope scope,
        SettingsPage page,
        SettingsPlace? place,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        if (!_homes.TryGetValue(page.Home, out var home))
        {
            throw new InvalidOperationException(
                $"Nothing is composed for {page.Home} settings, so page '{page.Id}' cannot be saved.");
        }

        if (SettingsInspector.Where(home, place) is not { } at)
        {
            throw new InvalidOperationException(
                $"{page.Home} settings have nowhere to be right now, so page '{page.Id}' cannot be saved.");
        }

        if (page.IsReadOnly || home.IsReadOnly)
        {
            throw new InvalidOperationException($"Page '{page.Id}' is read only.");
        }

        if (page.IsLayered != layer.HasValue)
        {
            throw new ArgumentException(
                page.IsLayered
                    ? $"Page '{page.Id}' is layered, so a layer has to be named."
                    : $"Page '{page.Id}' has one file, so no layer can be named.",
                nameof(layer));
        }

        // A window can only write what it drew, so a key the page does not declare is a
        // mistake rather than a value nobody will ever see again.
        foreach (var edit in edits)
        {
            var declared = Find(page, edit.Key)
                ?? throw new ArgumentException(
                    $"Page '{page.Id}' does not declare '{edit.Key}'.",
                    nameof(edits));

            if (declared.IsReadOnly)
            {
                throw new ArgumentException($"'{edit.Key}' is read only.", nameof(edits));
            }
        }

        home.Apply(at, scope, layer, edits);
    }

    private static ISettingDescriptor? Find(SettingsPage page, string key)
    {
        foreach (var descriptor in page.Descriptors)
        {
            if (string.Equals(descriptor.Key, key, StringComparison.Ordinal))
            {
                return descriptor;
            }
        }

        return null;
    }
}
