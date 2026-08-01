namespace Workbench.Core.IO;

public sealed class SystemEnvironment : IEnvironment
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
