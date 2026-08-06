using Kitbash.Core.Settings;

namespace Kitbash.Settings;

internal sealed class AfterLaunchSettings : IAfterLaunchSettings
{
    private readonly AfterLaunchSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public AfterLaunchSettings(AfterLaunchSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public AfterLaunchAction AfterProjectManager => _schema.AfterProjectManager.Read(_settings.Global);

    public AfterLaunchAction AfterEditor => _schema.AfterEditor.Read(_settings.Global);

    public AfterLaunchAction AfterPlay => _schema.AfterPlay.Read(_settings.Global);

    public AfterLaunchAction AfterExternalTool => _schema.AfterExternalTool.Read(_settings.Global);
}
