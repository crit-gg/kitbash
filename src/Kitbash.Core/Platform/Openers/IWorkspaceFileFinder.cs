namespace Kitbash.Core.Platform.Openers;

/// <summary>Finds the files inside a workspace that a tool would open.</summary>
public interface IWorkspaceFileFinder
{
    /// <summary>
    /// Full paths under the root ending in one of the extensions, ordered by name. Dot
    /// directories and node_modules are skipped, and the walk stops at maxDepth.
    /// </summary>
    /// <param name="extensions">Each with its leading dot, matched ignoring case.</param>
    /// <param name="maxDepth">How far below the root to look. One is the root alone.</param>
    IReadOnlyList<string> Find(string root, IReadOnlyList<string> extensions, int maxDepth);
}
