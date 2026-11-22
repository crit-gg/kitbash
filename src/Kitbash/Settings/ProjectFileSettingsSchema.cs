using Kitbash.Core.Settings.Schema;

namespace Kitbash.Settings;

/// <summary>What opens a <c>project.godot</c> on this machine. Drawn on the Launcher page.</summary>
public sealed class ProjectFileSettingsSchema
{
    public ProjectFileSettingsSchema()
    {
        OpenByDefault = new SettingDescriptor<bool>
        {
            Key = "launcher.projectFiles.default",
            Name = "Open project files with Kitbash",
            Description = "Kitbash is always offered in Open with. This makes it what a double click on project.godot uses.",
            Default = false,
        };

        Section = new SettingsSection("Godot project files", [OpenByDefault]);
    }

    public SettingDescriptor<bool> OpenByDefault { get; }

    public SettingsSection Section { get; }
}
