namespace Workbench.Core.Settings.Schema;

internal sealed class SettingsInspector : ISettingsInspector
{
    private readonly IReadOnlyDictionary<SettingsHome, ISettingsHome> _homes;
    private readonly ISettingsDocumentStore _store;
    private readonly ISettingsValueConverter _converter;

    public SettingsInspector(
        IEnumerable<ISettingsHome> homes,
        ISettingsDocumentStore store,
        ISettingsValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(homes);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(converter);

        // Last one registered wins, so a consumer can substitute a home.
        var byHome = new Dictionary<SettingsHome, ISettingsHome>();

        foreach (var home in homes)
        {
            byHome[home.Home] = home;
        }

        _homes = byHome;
        _store = store;
        _converter = converter;
    }

    public IReadOnlyList<SettingsPlace> PlacesIn(SettingsHome home) =>
        _homes.TryGetValue(home, out var found) ? found.Places : [];

    public SettingsPageView Read(SettingsScope scope, SettingsPage page, SettingsPlace? place = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!_homes.TryGetValue(page.Home, out var home) || Where(home, place) is not { } at)
        {
            return new SettingsPageView(page, IsAvailable: false, IsWritable: false, [], []);
        }

        var files = new List<SettingsFileView>(home.Layers.Count);
        var documents = new List<(SettingsLayer? Layer, SettingsDocument Document)>(home.Layers.Count);

        // Lowest precedence first, the order the home declares.
        foreach (var layer in home.Layers)
        {
            var file = _store.Open(home.FileFor(at, scope, layer));

            files.Add(new SettingsFileView(layer, file.Path, file.Exists, file.ParseError));
            documents.Add((layer, file.Document));
        }

        var values = new List<SettingValueView>(page.Descriptors.Count);

        foreach (var descriptor in page.Descriptors)
        {
            values.Add(Judge(descriptor, documents));
        }

        return new SettingsPageView(
            page,
            IsAvailable: true,
            IsWritable: !page.IsReadOnly && !home.IsReadOnly,
            files,
            values);
    }

    /// <summary>
    /// The place asked for, or the only one when none was named. Null when the home has
    /// none, or when the one asked for has gone since the caller last looked.
    /// </summary>
    internal static SettingsPlace? Where(ISettingsHome home, SettingsPlace? place)
    {
        var places = home.Places;

        if (places.Count == 0)
        {
            return null;
        }

        if (place is null)
        {
            return places[0];
        }

        foreach (var candidate in places)
        {
            if (string.Equals(candidate.Id, place.Id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private SettingValueView Judge(
        ISettingDescriptor descriptor,
        IReadOnlyList<(SettingsLayer? Layer, SettingsDocument Document)> documents)
    {
        var layers = new List<SettingLayerValue>(documents.Count);
        var converted = new object?[documents.Count];

        for (var index = 0; index < documents.Count; index++)
        {
            var (layer, document) = documents[index];

            var check = document.TryGetValue(descriptor.Key, out var raw)
                ? descriptor.Check(raw, _converter, out converted[index])
                : SettingCheck.Absent;

            layers.Add(new SettingLayerValue(layer, raw, check));
        }

        var effective = descriptor.Default;
        var origin = SettingOrigin.Default;
        string? problem = null;

        // Highest precedence first, so the first usable value is the one in force.
        for (var index = layers.Count - 1; index >= 0; index--)
        {
            var candidate = layers[index];

            if (problem is null && candidate.Check.IsProblem)
            {
                problem = candidate.Check.Message;
            }

            if (origin is SettingOrigin.Default && candidate.Check.IsUsable)
            {
                effective = converted[index]!;
                origin = OriginOf(candidate.Layer);
            }
        }

        // A stored value that did not survive outranks the origin, since the file and the
        // window now disagree about what the setting is.
        if (problem is not null)
        {
            origin = SettingOrigin.Invalid;
        }

        return new SettingValueView(
            descriptor,
            effective,
            origin,
            !descriptor.IsDefault(effective),
            problem,
            layers);
    }

    private static SettingOrigin OriginOf(SettingsLayer? layer) => layer switch
    {
        SettingsLayer.TeamShared => SettingOrigin.TeamShared,
        SettingsLayer.User => SettingOrigin.User,
        _ => SettingOrigin.File,
    };
}
