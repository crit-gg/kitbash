using Kitbash.Core.Settings;

namespace Kitbash.Settings;

/// <summary>Every launcher startup key, read from the application settings file.</summary>
public sealed class StartupSettings : IStartupSettings
{
    private readonly StartupSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public StartupSettings(StartupSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public StartPage StartPage => _schema.StartPage.Read(_settings.Global);
}
