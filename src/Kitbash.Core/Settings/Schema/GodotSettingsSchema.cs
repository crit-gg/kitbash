namespace Kitbash.Core.Settings.Schema;

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
            Description = "Where engines Kitbash installs go. Installed ones stay where they are.",
            Default = paths.Engines,
            Rules = [new PathShapeRule(PathKind.Directory, mustBeRooted: true)],
        };

        DefaultEngine = new SettingDescriptor<string>
        {
            Key = "godot.engines.default",
            Name = "Default engine",
            Description =
                "The engine a project uses when it pins no version, such as 4.7.1-stable. "
                + "Blank means this machine has none.",
            Default = string.Empty,
            Rules =
            [
                new PatternRule(
                    @"^$|^\d+\.\d+(\.[1-9]\d*)?-(stable|dev\d+|alpha\d+|beta\d+|rc\d+)(-mono)?$",
                    "an engine name such as 4.7.1-stable or 4.7.1-stable-mono, or blank for none"),
            ],
        };

        OnPath = new SettingDescriptor<bool>
        {
            Key = "godot.engines.onPath",
            Name = "Default engine on PATH",
            Description = "Lets a terminal run the default engine as godot.",
            Default = false,
        };

        BuildTool = new SettingDescriptor<string>
        {
            Key = "godot.build",
            Name = "Build C# before opening",
            Description = "What builds a project's C# when it opens. A project with no C# never builds.",
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

        Page = PageOf([EngineDirectory, DefaultEngine, OnPath]);
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
    /// Whether the default engine can be run as godot from a terminal. Off unless a person
    /// asks, since it changes their PATH.
    /// </summary>
    public SettingDescriptor<bool> OnPath { get; }

    /// <summary>
    /// Which program builds C# before a project opens. A closed set, so a value outside
    /// it is refused and the layer below decides.
    /// </summary>
    public SettingDescriptor<string> BuildTool { get; }

    public SettingsPage Page { get; }

    /// <summary>The same page with a row of the app's own under <see cref="OnPath"/>.</summary>
    public SettingsPage PageWith(ISettingsRow belowOnPath)
    {
        ArgumentNullException.ThrowIfNull(belowOnPath);

        return PageOf([EngineDirectory, DefaultEngine, OnPath, belowOnPath]);
    }

    private SettingsPage PageOf(IReadOnlyList<ISettingsRow> installs) => new()
    {
        Id = "godotEngines",
        Title = "Godot engines",
        Home = SettingsHome.Application,
        Sections =
        [
            new SettingsSection("Installs", installs),
            new SettingsSection("Opening a project", [BuildTool]),
        ],
    };
}
