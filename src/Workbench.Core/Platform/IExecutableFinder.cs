namespace Workbench.Core.Platform;

/// <summary>Looks up programs on PATH.</summary>
public interface IExecutableFinder
{
    /// <summary>Full path of the program, or null when it is not installed.</summary>
    string? Find(string fileName);
}
