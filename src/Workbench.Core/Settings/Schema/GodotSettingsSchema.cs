namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The Godot engine choices that belong to one person on one machine.
/// </summary>
/// <remarks>
/// The install directory's default is the real folder rather than blank, worked out from
/// <see cref="ApplicationPaths"/> when this is built. So a window shows where engines
/// actually go rather than an empty box, and resetting the setting means that folder
/// rather than meaning nothing. A path override that has no knowable default, such as a
/// program found on PATH, cannot do this and uses blank instead.
/// </remarks>
public sealed class GodotSettingsSchema
{
    public GodotSettingsSchema(ApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        EngineDirectory = new SettingDescriptor<string>
        {
            Key = "godot.engines.directory",
            Name = "Engine install directory",
            Description =
                "Where Godot engines Workbench installs are kept. Engines already installed "
                + "stay where they are, so moving this does not move them.",
            Default = paths.Engines,
            Rules = [new PathShapeRule(PathKind.Directory, mustBeRooted: true)],
        };

        Page = new SettingsPage
        {
            Id = "godotEngines",
            Title = "Godot engines",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Installs", [EngineDirectory])],
        };
    }

    /// <summary>Never blank. Defaults to the engines folder under the data directory.</summary>
    public SettingDescriptor<string> EngineDirectory { get; }

    public SettingsPage Page { get; }
}
