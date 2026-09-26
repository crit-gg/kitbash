using Kitbash.Core.Settings.Schema;

namespace Kitbash.Settings;

/// <summary>How the launcher comes up. Drawn as the first section of the Launcher page.</summary>
public sealed class StartupSettingsSchema
{
    public StartupSettingsSchema()
    {
        StartPage = new SettingDescriptor<StartPage>
        {
            Key = "launcher.startPage",
            Name = "Open on",
            Description = "The page Kitbash shows when it starts. With no workspace open, it "
                + "shows the list.",
            Default = Settings.StartPage.OpenWorkspace,
            Editor = SettingEditor.Segment,
            Rules =
            [
                new ChoiceRule<StartPage>(
                [
                    new SettingChoice<StartPage>(
                        Settings.StartPage.AllWorkspaces, "All workspaces", "Every workspace in a list"),
                    new SettingChoice<StartPage>(
                        Settings.StartPage.OpenWorkspace, "Open workspace", "The workspace open last time"),
                ]),
            ],
        };

        Section = new SettingsSection("When Kitbash starts", [StartPage]);
    }

    public SettingDescriptor<StartPage> StartPage { get; }

    public SettingsSection Section { get; }
}
