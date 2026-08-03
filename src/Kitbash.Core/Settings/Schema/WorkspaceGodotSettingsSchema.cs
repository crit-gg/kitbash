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
                "The version this workspace needs, such as 4.7 or 4.7.1-stable-mono. Blank "
                + "follows project.godot.",
            Default = string.Empty,
            Rules =
            [
                new PatternRule(
                    @"^$|^\d+(\.\d+(\.\d+)?)?(-(stable|(dev|alpha|beta|rc)\d*)(-mono)?)?$",
                    "a Godot version such as 4.7, 4.7.1 or 4.7.1-stable-mono, or blank for none"),
            ],
        };

        Page = new SettingsPage
        {
            Id = "workspaceGodot",
            Title = "Godot",
            Home = SettingsHome.Workspace,
            Sections = [new SettingsSection("Engine", [Engine])],
        };
    }

    /// <summary>
    /// The version pin. Blank is a real answer and means the project decides, which is
    /// what <c>config/features</c> in its <c>project.godot</c> already says.
    /// </summary>
    public SettingDescriptor<string> Engine { get; }

    public SettingsPage Page { get; }
}
