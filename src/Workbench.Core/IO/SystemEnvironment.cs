namespace Workbench.Core.IO;

public sealed class SystemEnvironment : IEnvironment
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    // Resolves to XDG_CONFIG_HOME or ~/.config on Linux, and AppData on Windows.
    public string GetConfigurationDirectory() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
