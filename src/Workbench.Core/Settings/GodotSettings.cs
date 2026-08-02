using Workbench.Core.Settings.Schema;

namespace Workbench.Core.Settings;

internal sealed class GodotSettings : IGodotSettings
{
    private readonly GodotSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public GodotSettings(GodotSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public string EngineDirectory
    {
        get
        {
            var stored = _schema.EngineDirectory.Read(_settings.Global);

            // The rule refuses blank, so a stored blank never reaches here. A file edited
            // by hand can still hold one, and the default is the honest answer to it.
            return string.IsNullOrWhiteSpace(stored) ? _schema.EngineDirectory.Default : stored;
        }
    }

    public void SetEngineDirectory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _settings.Set(SettingsScope.Global, _schema.EngineDirectory.Key, value);
    }
}
