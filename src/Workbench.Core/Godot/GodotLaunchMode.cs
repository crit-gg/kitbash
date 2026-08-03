namespace Workbench.Core.Godot;

/// <summary>What a project is being opened for.</summary>
/// <remarks>
/// **Running is the default and editing is the flag.** Godot 4 runs the project when it
/// is handed a path, and <c>--editor</c> is what asks for the editor instead. Read off
/// <c>--help</c> on 4.7.1: "-e, --editor  Start the editor instead of running the scene."
/// There is no flag for running, and nothing in the 129 lines of that help matches
/// <c>-g</c> or <c>--game</c>.
///
/// Everything before the last step is the same either way. A project that will not build
/// will not run, and one that was never imported has no resources to run with, so both
/// modes build and import first.
/// </remarks>
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
