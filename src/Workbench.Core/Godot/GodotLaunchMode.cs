namespace Workbench.Core.Godot;

/// <summary>What a project is being opened for.</summary>
public enum GodotLaunchMode
{
    /// <summary>Open the editor, which is <c>--editor</c>.</summary>
    Editor,

    /// <summary>Run the project, which is no flag at all.</summary>
    Play,

    /// <summary>
    /// Throw the import cache away and make it again. Deletes <c>.godot</c>, builds, and
    /// imports everything, then stops rather than starting anything.
    /// </summary>
    Rebuild,
}
