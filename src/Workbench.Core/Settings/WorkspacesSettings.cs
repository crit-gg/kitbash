using Workbench.Core.Settings.Schema;

namespace Workbench.Core.Settings;

internal sealed class WorkspacesSettings : IWorkspacesSettings
{
    private readonly WorkspacesSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public WorkspacesSettings(WorkspacesSettingsSchema schema, IApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(settings);

        _schema = schema;
        _settings = settings;
    }

    public string DefaultDirectory
    {
        get
        {
            var stored = _schema.Directory.Read(_settings.Global);

            // The rule allows blank, and a relative path from a hand edited file is not a
            // place, so both read as no default.
            return string.IsNullOrWhiteSpace(stored) || !Path.IsPathRooted(stored)
                ? string.Empty
                : stored;
        }
    }

    public void SetDefaultDirectory(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _settings.Apply(
            SettingsScope.Global,
            string.IsNullOrWhiteSpace(value)
                ? [SettingsEdit.Remove(_schema.Directory.Key)]
                : [SettingsEdit.Set(_schema.Directory.Key, value)]);
    }
}
