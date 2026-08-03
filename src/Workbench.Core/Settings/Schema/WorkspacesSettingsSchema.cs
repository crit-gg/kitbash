namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Where this person keeps their workspaces on this machine.
/// </summary>
public sealed class WorkspacesSettingsSchema
{
    public WorkspacesSettingsSchema()
    {
        Directory = new SettingDescriptor<string>
        {
            Key = "workspaces.directory",
            Name = "Workspaces folder",
            Description =
                "Where Workbench offers to put a workspace it clones from git. Blank means "
                + "it has no suggestion and the folder is picked each time. Workspaces "
                + "already added stay where they are, so changing this does not move them.",
            Default = string.Empty,
            Rules = [new PathShapeRule(PathKind.Directory, mustBeRooted: true, allowEmpty: true)],
        };

        Page = new SettingsPage
        {
            Id = "workspaces",
            Title = "Workspaces",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Cloning", [Directory])],
        };
    }

    /// <summary>Blank is a real answer meaning this machine has no usual place for them.</summary>
    public SettingDescriptor<string> Directory { get; }

    public SettingsPage Page { get; }
}
