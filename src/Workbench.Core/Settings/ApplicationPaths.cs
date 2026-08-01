using Workbench.Core.IO;

namespace Workbench.Core.Settings;

/// <summary>
/// Where settings for this user on this machine live. These sit outside any
/// workspace, so they can be read before a workspace is known.
/// </summary>
public sealed class ApplicationPaths
{
    private const string GlobalFileName = "workbench.toml";
    private const string ToolsDirectoryName = "tools";

    public ApplicationPaths(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Root = Path.Combine(environment.GetConfigurationDirectory(), "Workbench");
    }

    public string Root { get; }

    /// <summary>The file backing one scope. The file need not exist.</summary>
    public string FileFor(SettingsScope scope)
    {
        if (scope.IsGlobal)
        {
            return Path.Combine(Root, GlobalFileName);
        }

        return Path.Combine(Root, ToolsDirectoryName, scope.ToolId + ".toml");
    }
}
