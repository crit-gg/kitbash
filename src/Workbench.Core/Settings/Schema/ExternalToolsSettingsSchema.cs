namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Where the programs Workbench runs actually are. Both are overrides, so blank is the
/// ordinary answer and means the app looks the program up on PATH the way it always has.
/// </summary>
public sealed class ExternalToolsSettingsSchema
{
    public ExternalToolsSettingsSchema()
    {
        GitPath = new SettingDescriptor<string>
        {
            Key = "tools.git.path",
            Name = "Git Executable",
            Description =
                "Full path to the git to run. Leave blank to use the first git on PATH, "
                + "which is what a terminal on this machine would use.",
            Default = string.Empty,
            NeedsRestart = true,
            Rules = [new PathShapeRule(PathKind.File, true, true)]
        };

        GitClientPath = new SettingDescriptor<string>
        {
            Key = "tools.gitClient.path",
            Name = "Git Client",
            Description =
                "Full path to the git client to run. Leave blank to use no git client.",
            Default = string.Empty,
            NeedsRestart = true,
            Rules = [new PathShapeRule(PathKind.File, true, true)]
        };

        DotnetPath = new SettingDescriptor<string>
        {
            Key = "tools.dotnet.path",
            Name = "Dotnet Executable",
            Description =
                "Full path to the dotnet to run. Leave blank to use the first dotnet on PATH.",
            Default = string.Empty,
            NeedsRestart = true,
            Rules = [new PathShapeRule(PathKind.File, true, true)]
        };

        Page = new SettingsPage
        {
            Id = "externalTools",
            Title = "External tools",
            Home = SettingsHome.Application,
            Sections = [
                new SettingsSection("Git", [GitPath, GitClientPath]),
                new SettingsSection(".NET", [DotnetPath])
            ]
        };
    }

    /// <summary>Blank means no git client has been configured.</summary>
    public SettingDescriptor<string> GitClientPath { get; set; }

    /// <summary>Blank means the git on PATH, which is what the app did before this existed.</summary>
    public SettingDescriptor<string> GitPath { get; }

    /// <summary>Blank means the dotnet on PATH.</summary>
    public SettingDescriptor<string> DotnetPath { get; }

    public SettingsPage Page { get; }
}