namespace Kitbash.Core.Godot;

/// <summary>Whether a project's assets have been imported.</summary>
public interface IGodotImports
{
    /// <summary>
    /// True when anything an import sidecar declares is not on disk. Walks the project,
    /// so keep it off the UI thread.
    /// </summary>
    bool NeedsImport(GodotProject project);
}
