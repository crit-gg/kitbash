using Kitbash.Core.Settings;

namespace Kitbash.Settings;

/// <summary>The developer key, read from the application settings file.</summary>
public sealed class DeveloperSettings : IDeveloperSettings
{
    private readonly DeveloperSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public DeveloperSettings(DeveloperSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public bool ShowsDeveloperFeatures => _schema.ShowDeveloperFeatures.Read(_settings.Global);
}
