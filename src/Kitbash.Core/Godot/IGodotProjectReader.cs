namespace Kitbash.Core.Godot;

/// <summary>Finds and reads a <c>project.godot</c>.</summary>
public interface IGodotProjectReader
{
    /// <summary>
    /// The nearest Godot project at or under a folder, or null when there is none. The
    /// search is bounded, so a folder that is not a game project costs little.
    /// </summary>
    GodotProject? Find(string root);

    /// <summary>
    /// Reads a <c>project.godot</c> whose path is already known. A file that will not
    /// read leaves every value at its default rather than throwing, since a project this
    /// app did not write is not this app's to refuse.
    /// </summary>
    GodotProject Read(string projectFile);
}
