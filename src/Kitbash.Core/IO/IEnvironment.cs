namespace Kitbash.Core.IO;

/// <summary>Process environment. Kept behind an interface so lookups stay replaceable.</summary>
public interface IEnvironment
{
    string? GetVariable(string name);

    /// <summary>The user's home directory.</summary>
    string GetHomeDirectory();
}
