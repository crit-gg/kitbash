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

    public SettingsPageView Read(SettingsScope scope, SettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!_homes.TryGetValue(page.Home, out var home))
        {
            return new SettingsPageView(page, IsAvailable: false, [], []);
        }

        var files = new List<SettingsFileView>(home.Layers.Count);
        var documents = new List<(SettingsLayer? Layer, SettingsDocument Document)>(home.Layers.Count);

        // Lowest precedence first, the order the home declares.
        foreach (var layer in home.Layers)
        {
            var file = _store.Open(home.FileFor(scope, layer));

            files.Add(new SettingsFileView(layer, file.Path, file.Exists, file.ParseError));
            documents.Add((layer, file.Document));
        }

        var values = new List<SettingValueView>(page.Descriptors.Count);

        foreach (var descriptor in page.Descriptors)
        {
            values.Add(Judge(descriptor, documents));
        }

        return new SettingsPageView(page, IsAvailable: true, files, values);
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
