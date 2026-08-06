namespace Kitbash.Core.IO;

public sealed class SystemEnvironment : IEnvironment
{
    private const string DotnetHost = "dotnet";

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetHomeDirectory() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public IReadOnlyList<string> GetProcessCommand()
    {
        if (Environment.ProcessPath is not { Length: > 0 } path)
        {
            return [];
        }

        // An app started as dotnet app.dll reports the host as its path, so the assembly
        // goes back on the front or the command names dotnet and nothing else.
        if (!string.Equals(
                Path.GetFileNameWithoutExtension(path), DotnetHost, StringComparison.OrdinalIgnoreCase))
        {
            return [path];
        }

        var given = Environment.GetCommandLineArgs();

        return given is [{ Length: > 0 } assembly, ..] ? [path, assembly] : [path];
    }
}
