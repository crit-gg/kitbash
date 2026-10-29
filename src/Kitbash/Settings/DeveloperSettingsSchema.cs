using Kitbash.Core.Settings.Schema;

namespace Kitbash.Settings;

/// <summary>What the launcher offers the people who make patches and links. Drawn last on the Launcher page.</summary>
public sealed class DeveloperSettingsSchema
{
    public DeveloperSettingsSchema()
    {
        ShowDeveloperFeatures = new SettingDescriptor<bool>
        {
            Key = "launcher.developer",
            Name = "Show developer features",
            Description = "Adds tools for the people who make patches and links, such as Copy patch link.",
            Default = false,
        };

        Section = new SettingsSection("Advanced", [ShowDeveloperFeatures]);
    }

    public SettingDescriptor<bool> ShowDeveloperFeatures { get; }

    public SettingsSection Section { get; }
}
