using Kitbash.Core.Settings;
using Kitbash.Tools;

namespace Kitbash.Settings;

/// <summary>Every launcher.after key, read from the application settings file.</summary>
public sealed class AfterLaunchSettings : IAfterLaunchSettings
{
    private readonly AfterLaunchSettingsSchema _schema;
    private readonly IApplicationSettings _settings;
    private readonly IAfterLaunchOverrides _overrides;

    public AfterLaunchSettings(
        AfterLaunchSettingsSchema schema,
        IApplicationSettings settings,
        IAfterLaunchOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(overrides);

        _schema = schema;
        _settings = settings;
        _overrides = overrides;
    }

    public AfterLaunchAction AfterProjectManager => _schema.AfterProjectManager.Read(_settings.Global);

    public AfterLaunchAction AfterEditor => _schema.AfterEditor.Read(_settings.Global);

    public AfterLaunchAction AfterPlay => _schema.AfterPlay.Read(_settings.Global);

    public AfterLaunchAction AfterExternalTool => _schema.AfterExternalTool.Read(_settings.Global);

    public AfterLaunchAction AfterTool => _schema.AfterTool.Read(_settings.Global);

    // The list is read first, since it rereads the file and the default is read from it.
    public AfterLaunchAction ForTool(ToolId id) =>
        _overrides.Read().FirstOrDefault(chosen => chosen.Id == id) is { } own
            ? own.Action
            : AfterTool;
}
