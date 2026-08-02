namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Where the programs Workbench runs actually are. Both are overrides, so blank is the
/// ordinary answer and means the app looks the program up on PATH the way it always has.
/// </summary>
/// <remarks>
/// These live in <see cref="SettingsHome.Application"/>, which is per user per machine
/// and has no layer. That is not a convenience. A path names a place on one machine, so a
/// team shared one would be wrong for everybody who did not write it, and this home is
/// the only one where it cannot be shared by accident.
/// </remarks>
public sealed class ExternalToolsSettingsSchema
{
    public ExternalToolsSettingsSchema()
    {
        GitPath = new SettingDescriptor<string>
        {
            Key = "tools.git.path",
            Name = "Git program",
            Description =
                "Full path to the git to run. Leave blank to use the first git on PATH, "
                + "which is what a terminal on this machine would use. Takes effect at the next launch.",
            Default = string.Empty,
            Rules = [new PathShapeRule(PathKind.File, mustBeRooted: true, allowEmpty: true)],
        };

        DotnetPath = new SettingDescriptor<string>
        {
            Key = "tools.dotnet.path",
            Name = "dotnet program",
            Description =
                "Full path to the dotnet to run. Leave blank to use the first dotnet on PATH. "
                + "Takes effect at the next launch.",
            Default = string.Empty,
            Rules = [new PathShapeRule(PathKind.File, mustBeRooted: true, allowEmpty: true)],
        };

        Page = new SettingsPage
        {
            Id = "externalTools",
            Title = "External tools",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Programs", [GitPath, DotnetPath])],
        };
    }

    /// <summary>Blank means the git on PATH, which is what the app did before this existed.</summary>
    public SettingDescriptor<string> GitPath { get; }

    /// <summary>Blank means the dotnet on PATH.</summary>
    public SettingDescriptor<string> DotnetPath { get; }

    public SettingsPage Page { get; }
}
