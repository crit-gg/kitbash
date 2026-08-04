using Kitbash.Core.Settings.Schema;

namespace Kitbash.Settings;

/// <summary>
/// Whether the launcher closes itself once it has started something in Godot. The
/// launcher's own keys, since nothing but the launcher can close the launcher.
/// </summary>
public sealed class LauncherCloseSettingsSchema
{
    public LauncherCloseSettingsSchema()
    {
        AfterProjectManager = new SettingDescriptor<bool>
        {
            Key = "launcher.close.projectManager",
            Name = "Close after opening the project manager",
            Description = "Close Kitbash once a Godot project manager has started.",
            Default = false,
        };

        AfterEditor = new SettingDescriptor<bool>
        {
            Key = "launcher.close.editor",
            Name = "Close after opening a project in the editor",
            Description =
                "Close Kitbash once the editor has started. A launch that failed or was "
                + "cancelled leaves it open.",
            Default = false,
        };

        AfterPlay = new SettingDescriptor<bool>
        {
            Key = "launcher.close.play",
            Name = "Close after playing a project",
            Description =
                "Close Kitbash once the project has started. A launch that failed or was "
                + "cancelled leaves it open.",
            Default = false,
        };

        Page = new SettingsPage
        {
            Id = "launcher",
            Title = "Launcher",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection(
                    "After starting Godot",
                    [AfterProjectManager, AfterEditor, AfterPlay]),
            ],
        };
    }

    /// <summary>Follows the project manager the engines page starts.</summary>
    public SettingDescriptor<bool> AfterProjectManager { get; }

    /// <summary>Follows a project opened in the editor, however it was opened.</summary>
    public SettingDescriptor<bool> AfterEditor { get; }

    /// <summary>Follows a project run rather than edited. A rebuild starts nothing, so it never closes.</summary>
    public SettingDescriptor<bool> AfterPlay { get; }

    public SettingsPage Page { get; }
}
