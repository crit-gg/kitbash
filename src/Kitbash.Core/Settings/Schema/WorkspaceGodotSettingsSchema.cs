namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// The Godot choices that belong to a workspace rather than to a machine.
/// </summary>
public sealed class WorkspaceGodotSettingsSchema
{
    public WorkspaceGodotSettingsSchema()
    {
        Engine = new SettingDescriptor<string>
        {
            Key = "godot.engine",
            Name = "Engine version",
            Description =
                "The version this workspace needs, such as 4.7 or 4.7.1-stable-mono. With an "
                + "engine repository, 4.7.2 follows its newest build and 4.7.2+18d5d19 names one. "
                + "Blank follows project.godot.",
            Default = string.Empty,
            Rules =
            [
                new PatternRule(
                    @"^$|^\d+(\.\d+(\.\d+)?)?(-(stable|(dev|alpha|beta|rc)\d*)(-mono)?)?$"
                    + @"|^\d+\.\d+(\.\d+)?(\+[0-9A-Za-z_.]+)?(-mono)?$",
                    "a Godot version such as 4.7, 4.7.1 or 4.7.1-stable-mono, or blank for none"),
            ],
        };

        Repository = new SettingDescriptor<string>
        {
            Key = "godot.repository",
            Name = "Engine repository",
            Description =
                "Where this workspace gets its engine, by the name its repository was given. "
                + "Blank uses official Godot builds.",
            Default = string.Empty,
        };

        Page = new SettingsPage
        {
            Id = "workspaceGodot",
            Title = "Godot",
            Home = SettingsHome.Workspace,
            Sections = [new SettingsSection("Engine", [Engine, Repository])],
        };
    }

    /// <summary>
    /// The version pin. Blank is a real answer and means the project decides, which is
    /// what <c>config/features</c> in its <c>project.godot</c> already says.
    /// </summary>
    public SettingDescriptor<string> Engine { get; }

    /// <summary>
    /// A name from <c>godot.repositories</c>. Blank is a real answer and means official
    /// builds, which is every workspace made before repositories existed.
    /// </summary>
    public SettingDescriptor<string> Repository { get; }

    public SettingsPage Page { get; }
}
