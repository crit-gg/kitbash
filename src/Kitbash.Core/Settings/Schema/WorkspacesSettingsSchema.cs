namespace Kitbash.Core.Settings.Schema;

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
                "Where Kitbash offers to put a workspace it creates or clones. Blank "
                + "means it suggests nothing.",
            Default = string.Empty,
            Rules = [new PathShapeRule(PathKind.Directory, mustBeRooted: true, allowEmpty: true)],
        };

        Page = new SettingsPage
        {
            Id = "workspaces",
            Title = "Workspaces",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("New workspaces", [Directory])],
        };
    }

    /// <summary>Blank is a real answer meaning this machine has no usual place for them.</summary>
    public SettingDescriptor<string> Directory { get; }

    public SettingsPage Page { get; }
}
