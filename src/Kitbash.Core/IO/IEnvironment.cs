namespace Kitbash.Core.IO;

/// <summary>Process environment. Kept behind an interface so lookups stay replaceable.</summary>
public interface IEnvironment
{
    string? GetVariable(string name);

    /// <summary>The user's home directory.</summary>
    string GetHomeDirectory();

    /// <summary>
    /// How to run this program again, as the words before any argument of its own. Empty
    /// when the path cannot be told, which is what a single file host can answer.
    /// </summary>
    IReadOnlyList<string> GetProcessCommand();
}
