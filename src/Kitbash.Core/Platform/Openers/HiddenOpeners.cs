using Kitbash.Core.Settings;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// <c>tools.hidden</c> is a list of opener ids, which no page declares, so it is read and
/// written directly the way the custom tool list is.
/// </summary>
internal sealed class HiddenOpeners : IHiddenOpeners
{
    private const string Key = "tools.hidden";

    private readonly IApplicationSettings _settings;

    public HiddenOpeners(IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public IReadOnlyList<string> Read()
    {
        // The file has not been read since the last write from here, so it is read again.
        _settings.Reload();

        if (!_settings.Global.TryGet<object[]>(Key, out var rows))
        {
            return [];
        }

        // An id is compared ordinally, since it is written by the app rather than typed.
        return
        [
            .. rows.OfType<string>()
                .Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];
    }

    public void Write(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        string[] rows =
        [
            .. ids.Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];

        _settings.Apply(SettingsScope.Global, [SettingsEdit.Set(Key, rows)]);
    }
}
