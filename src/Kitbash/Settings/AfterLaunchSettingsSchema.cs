using Kitbash.Core.Settings.Schema;

namespace Kitbash.Settings;

/// <summary>
/// What the launcher does with itself once it has started something. The launcher's own
/// keys, since nothing but the launcher can minimize or close the launcher.
/// </summary>
public sealed class AfterLaunchSettingsSchema
{
    public AfterLaunchSettingsSchema()
    {
        AfterProjectManager = Action(
            "launcher.after.projectManager",
            "After opening the project manager",
            "What Kitbash does once a Godot project manager has started.");

        AfterEditor = Action(
            "launcher.after.editor",
            "After opening a project in the editor",
            "What Kitbash does once the editor has started. A launch that failed or was "
            + "cancelled leaves it alone.");

        AfterPlay = Action(
            "launcher.after.play",
            "After playing a project",
            "What Kitbash does once the project has started. A launch that failed or was "
            + "cancelled leaves it alone.");

        AfterExternalTool = Action(
            "launcher.after.externalTool",
            "After opening an external tool",
            "What Kitbash does once a tool from the Open in menu has started. Opening the "
            + "workspace folder is not one of them.");

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
                new SettingsSection("After starting a tool", [AfterExternalTool]),
            ],
        };
    }

    /// <summary>Follows the project manager the engines page starts.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterProjectManager { get; }

    /// <summary>Follows a project opened in the editor, however it was opened.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterEditor { get; }

    /// <summary>Follows a project run rather than edited. A rebuild starts nothing, so it never follows.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterPlay { get; }

    /// <summary>Follows an editor, a terminal or a custom tool from the Open in menu.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterExternalTool { get; }

    public SettingsPage Page { get; }

    private static SettingDescriptor<AfterLaunchAction> Action(
        string key,
        string name,
        string description) => new()
        {
            Key = key,
            Name = name,
            Description = description,
            Default = AfterLaunchAction.DoNothing,
            Rules =
            [
                new ChoiceRule<AfterLaunchAction>(
                [
                    new SettingChoice<AfterLaunchAction>(
                        AfterLaunchAction.DoNothing, "Do nothing", "Leave the launcher where it is"),
                    new SettingChoice<AfterLaunchAction>(
                        AfterLaunchAction.Minimize, "Minimize", "Send the launcher window down"),
                    new SettingChoice<AfterLaunchAction>(
                        AfterLaunchAction.Close, "Close", "End the launcher"),
                ]),
            ],
        };
}
