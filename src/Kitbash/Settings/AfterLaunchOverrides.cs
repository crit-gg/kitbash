using Kitbash.Core.Settings;
using Kitbash.Tools;

namespace Kitbash.Settings;

/// <summary>The per tool answers, as the array of tables the settings file holds.</summary>
public sealed class AfterLaunchOverrides : IAfterLaunchOverrides
{
    private const string Key = "launcher.after.tools";

    private const string IdKey = "id";
    private const string ActionKey = "action";

    private readonly IApplicationSettings _settings;

    public AfterLaunchOverrides(IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public IReadOnlyList<AfterLaunchOverride> Read()
    {
        // The file has not been read since the last write from here, so it is read again.
        _settings.Reload();

        if (!_settings.Global.TryGet<object[]>(Key, out var rows))
        {
            return [];
        }

        List<AfterLaunchOverride> overrides = [];

        // An array of tables reads back as an array of tables, so a row is a dictionary
        // rather than a type the converter knows. A row naming neither is skipped.
        foreach (var row in rows.OfType<IReadOnlyDictionary<string, object?>>())
        {
            if (Id(row) is not { } id || Action(row) is not { } action)
            {
                continue;
            }

            overrides.Add(new AfterLaunchOverride(id, action));
        }

        return overrides;
    }

    public void Write(IReadOnlyList<AfterLaunchOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var rows = overrides
            .Select(chosen => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [IdKey] = chosen.Id.Value,
                [ActionKey] = chosen.Action,
            })
            .ToArray();

        _settings.Apply(SettingsScope.Global, [SettingsEdit.Set(Key, rows)]);
    }

    private static ToolId? Id(IReadOnlyDictionary<string, object?> row) =>
        row.TryGetValue(IdKey, out var value) && value is string text && ToolId.TryParse(text, out var id)
            ? id
            : null;

    private static AfterLaunchAction? Action(IReadOnlyDictionary<string, object?> row) =>
        row.TryGetValue(ActionKey, out var value)
        && value is string text
        && Enum.TryParse<AfterLaunchAction>(text, ignoreCase: true, out var action)
            ? action
            : null;
}
