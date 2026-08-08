using Kitbash.Core.Settings.Schema;
using Kitbash.ViewModels;

namespace Kitbash.Settings;

/// <summary>
/// What the launcher does with itself once it has started something. The launcher's own
/// keys, since nothing but the launcher can minimize or close the launcher.
/// </summary>
public sealed class AfterLaunchSettingsSchema
{
    public AfterLaunchSettingsSchema(ToolActionsEditor tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        AfterProjectManager = Action(
            "launcher.after.projectManager",
            "When opening the project manager",
            "What Kitbash does once a Godot project manager has started.");

        AfterEditor = Action(
            "launcher.after.editor",
            "When opening a project in the editor",
            "What Kitbash does once the editor has started. A launch that failed or was "
            + "cancelled leaves it alone.");

        AfterPlay = Action(
            "launcher.after.play",
            "When playing a project",
            "What Kitbash does once the project has started. A launch that failed or was "
            + "cancelled leaves it alone.");

        AfterTool = Action(
            "launcher.after.tool",
            "When starting a tool",
            "What Kitbash does once a tool has started, for every tool that says nothing "
            + "of its own.");

        AfterExternalTool = Action(
            "launcher.after.externalTool",
            "When opening an external tool",
            "What Kitbash does once a tool from the Open in menu has started. Opening the "
            + "workspace folder is not one of them.");

        ToolsThatDiffer = new SettingsEditorRow
        {
            Name = "Tools",
            Description = "Every tool installed here. Default follows the setting above.",
            Layout = SettingsRowLayout.Below,
            Editor = tools,
        };

        Page = new SettingsPage
        {
            Id = "launcher",
            Title = "Launcher",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection(
                    "When starting Godot",
                    [AfterProjectManager, AfterEditor, AfterPlay]),
                new SettingsSection("When starting a Kitbash tool", [AfterTool, ToolsThatDiffer]),
                new SettingsSection("When opening an external tool", [AfterExternalTool]),
            ],
        };
    }

    /// <summary>Follows the project manager the engines page starts.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterProjectManager { get; }

    /// <summary>Follows a project opened in the editor, however it was opened.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterEditor { get; }

    /// <summary>Follows a project run rather than edited. A rebuild starts nothing, so it never follows.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterPlay { get; }

    /// <summary>Follows a tool the tools page starts, unless that tool has an answer of its own.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterTool { get; }

    /// <summary>Follows an editor, a terminal or a custom tool from the Open in menu.</summary>
    public SettingDescriptor<AfterLaunchAction> AfterExternalTool { get; }

    /// <summary>The per tool answers, which are an array of tables rather than a setting.</summary>
    public SettingsEditorRow ToolsThatDiffer { get; }

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