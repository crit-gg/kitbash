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

            // The rule refuses blank, but a hand edited file can still hold one, so it
            // falls back to the default.
            return string.IsNullOrWhiteSpace(stored) ? _schema.EngineDirectory.Default : stored;
        }
    }

    public Workbench.Core.Godot.EngineId? DefaultEngine
    {
        get
        {
            var stored = _schema.DefaultEngine.Read(_settings.Global);

            return Workbench.Core.Godot.EngineId.TryParse(stored, out var id) ? id : null;
        }
    }

    public void SetDefaultEngine(Workbench.Core.Godot.EngineId? value) =>
        _settings.Set(SettingsScope.Global, _schema.DefaultEngine.Key, value?.ToString() ?? string.Empty);

    public Workbench.Core.Godot.GodotBuildTool BuildTool =>
        _schema.BuildTool.Read(_settings.Global) switch
        {
            "dotnet" => Workbench.Core.Godot.GodotBuildTool.Dotnet,
            "editor" => Workbench.Core.Godot.GodotBuildTool.Editor,
            "off" => Workbench.Core.Godot.GodotBuildTool.None,

            // The rule refuses anything else, so this is the default arriving as itself.
            _ => Workbench.Core.Godot.GodotBuildTool.Auto,
        };

    public void SetEngineDirectory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _settings.Set(SettingsScope.Global, _schema.EngineDirectory.Key, value);
    }
}
