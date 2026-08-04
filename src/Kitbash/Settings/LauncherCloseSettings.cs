using Kitbash.Core.Settings;

namespace Kitbash.Settings;

internal sealed class LauncherCloseSettings : ILauncherCloseSettings
{
    private readonly LauncherCloseSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public LauncherCloseSettings(LauncherCloseSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public bool AfterProjectManager => _schema.AfterProjectManager.Read(_settings.Global);

    public bool AfterEditor => _schema.AfterEditor.Read(_settings.Global);

    public bool AfterPlay => _schema.AfterPlay.Read(_settings.Global);
}
