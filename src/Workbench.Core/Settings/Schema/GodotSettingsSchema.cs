namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The Godot engine choices that belong to one person on one machine.
/// </summary>
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

        BuildTool = new SettingDescriptor<string>
        {
            Key = "godot.build",
            Name = "Build C# before opening",
            Description =
                "What builds a project's C# when it is opened from Workbench. Automatic "
                + "uses dotnet when this machine has one and the Godot editor otherwise. "
                + "A project with no C# never builds, and neither does one opened in an "
                + "engine that has no C# support.",
            Default = AutoTool,
            Rules =
            [
                new ChoiceRule<string>(
                    [
                        new SettingChoice<string>(AutoTool, "Automatic", "dotnet when it is installed"),
                        new SettingChoice<string>("dotnet", "dotnet", "Faster, and says more when a build fails"),
                        new SettingChoice<string>("editor", "Godot editor", "Needs nothing else installed"),
                        new SettingChoice<string>("off", "Do not build", "Open with whatever is already built"),
                    ],
                    StringComparer.OrdinalIgnoreCase),
            ],
        };

        Page = new SettingsPage
        {
            Id = "godotEngines",
            Title = "Godot engines",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection("Installs", [EngineDirectory, DefaultEngine]),
                new SettingsSection("Opening a project", [BuildTool]),
            ],
        };
    }

    /// <summary>The value that means work it out from what is installed.</summary>
    public const string AutoTool = "auto";

    /// <summary>Never blank. Defaults to the engines folder under the data directory.</summary>
    public SettingDescriptor<string> EngineDirectory { get; }

    /// <summary>
    /// The install a project falls back to, by name. Blank is a real answer meaning there
    /// is none, which is what uninstalling the default leaves behind. Never set here.
    /// </summary>
    public SettingDescriptor<string> DefaultEngine { get; }

    /// <summary>
    /// Which program builds C# before a project opens. A closed set, so a value outside
    /// it is refused and the layer below decides.
    /// </summary>
    public SettingDescriptor<string> BuildTool { get; }

    public SettingsPage Page { get; }
}
