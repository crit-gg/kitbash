using Kitbash.Core.Settings.Schema;

namespace Kitbash.Core.Settings;

internal sealed class WindowSettings : IWindowSettings
{
    private readonly WindowSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    /// <summary>
    /// The schema arrives rather than being named, so the key and the default come from
    /// the one place that declares them and there is no static descriptor to reach for.
    /// </summary>
    public WindowSettings(WindowSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public bool UseNativeChrome => _schema.NativeChrome.Read(_settings.Global);

    public void SetUseNativeChrome(bool value) =>
        _settings.Set(SettingsScope.Global, _schema.NativeChrome.Key, value);
}
