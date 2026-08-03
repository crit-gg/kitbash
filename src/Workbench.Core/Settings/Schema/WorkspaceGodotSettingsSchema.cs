namespace Workbench.Core.Settings.Schema;

/// <summary>
/// The Godot choices that belong to a workspace rather than to a machine.
/// </summary>
/// <remarks>
/// <para>
/// **This is the one Godot page a team shares.** Which engine a project needs is a fact
/// about the project, so it belongs beside the project and is committed. Where engines
/// are installed and which one this machine falls back to are not, and they live in
/// <see cref="GodotSettingsSchema"/> under the application home where nothing can share
/// them by accident.
/// </para>
/// <para>
/// It layers, since the workspace home is the only one that does. A team pins the
/// version everybody needs and a person can hold a different one locally without
/// changing what the repository says.
/// </para>
/// </remarks>
public sealed class WorkspaceGodotSettingsSchema
{
    public WorkspaceGodotSettingsSchema()
    {
        Engine = new SettingDescriptor<string>
        {
            Key = "godot.engine",
            Name = "Engine version",
            Description =
                "Which Godot version this workspace needs. Any part may be left off, so "
                + "4.7 means any 4.7 release and 4.7.1-stable means exactly that one. End "
                + "it with mono to require the .NET build, which is how the engines page "
                + "names one. Blank uses the version the project names in project.godot.",
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
