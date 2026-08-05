namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Where the programs Kitbash runs actually are. Both are overrides, so blank is the
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
            Description = "The git to run. Blank uses the first git on PATH.",
            Default = string.Empty,
            NeedsRestart = true,
            Rules = [new PathShapeRule(PathKind.File, true, true)]
        };

        DotnetPath = new SettingDescriptor<string>
        {
            Key = "tools.dotnet.path",
            Name = "Dotnet Executable",
            Description = "The dotnet to run. Blank uses the first dotnet on PATH.",
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
                new SettingsSection("Git", [GitPath]),
                new SettingsSection(".NET", [DotnetPath])
            ]
        };
    }

    /// <summary>Blank means the git on PATH, which is what the app did before this existed.</summary>
    public SettingDescriptor<string> GitPath { get; }

    /// <summary>Blank means the dotnet on PATH.</summary>
    public SettingDescriptor<string> DotnetPath { get; }

    public SettingsPage Page { get; }
}