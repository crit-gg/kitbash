namespace Kitbash.Core.Godot;

/// <summary>
/// Writes the files Godot's own project dialog writes when it creates a project.
/// </summary>
public interface IGodotProjectWriter
{
    /// <summary>
    /// Writes <c>project.godot</c>, <c>icon.svg</c> and <c>.editorconfig</c>. Touches a
    /// disk, so keep it off the UI thread.
    /// </summary>
    /// <exception cref="IOException">A file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be written.</exception>
    void Write(NewGodotProject project);

    /// <summary>
    /// Writes the <c>.gitignore</c> and <c>.gitattributes</c> Godot writes for a project
    /// tracked in git. Only for a folder that has a Godot project in it.
    /// </summary>
    void WriteGitFiles(string directory);
}
