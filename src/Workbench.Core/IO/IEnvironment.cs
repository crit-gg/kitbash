namespace Workbench.Core.IO;

/// <summary>Process environment. Kept behind an interface so lookups stay replaceable.</summary>
public interface IEnvironment
{
    string? GetVariable(string name);

    /// <summary>The user's configuration directory for this machine.</summary>
    string GetConfigurationDirectory();
}
