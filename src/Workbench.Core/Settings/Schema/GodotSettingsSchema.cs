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

        DefaultEngine = new SettingDescriptor<string>
        {
            Key = "godot.engines.default",
            Name = "Default engine",
            Description =
                "Which install a project uses when it pins no version. Named the way an "
                + "engine is named, such as 4.7.1-stable or 4.7.1-stable-mono. Blank means "
                + "this machine has no default, which is what uninstalling the default "
                + "leaves behind.",
            Default = string.Empty,
            Rules =
            [
                new PatternRule(
                    @"^$|^\d+\.\d+(\.[1-9]\d*)?-(stable|dev\d+|alpha\d+|beta\d+|rc\d+)(-mono)?$",
                    "an engine name such as 4.7.1-stable or 4.7.1-stable-mono, or blank for none"),
            ],
        };

        Page = new SettingsPage
        {
            Id = "godotEngines",
            Title = "Godot engines",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Installs", [EngineDirectory, DefaultEngine])],
        };
    }

    /// <summary>Never blank. Defaults to the engines folder under the data directory.</summary>
    public SettingDescriptor<string> EngineDirectory { get; }

    /// <summary>
    /// The install a project falls back to, by name. Blank is a real answer and means
    /// there is none, which is the state uninstalling the default leaves the machine in.
    /// Choosing the next one is a deliberate act and never automatic.
    /// </summary>
    public SettingDescriptor<string> DefaultEngine { get; }

    public SettingsPage Page { get; }
}
