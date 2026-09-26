namespace Kitbash.Core.Godot;

/// <summary>
/// The engine repositories a workspace can name: <c>godot.repositories</c> in the
/// workspace's own config and in the global one. Touches a disk, so call it off the UI thread.
/// </summary>
public interface IEngineRepositoryList
{
    /// <summary>The global list alone, which is the half a person edits here.</summary>
    IReadOnlyList<EngineRepositorySource> ReadGlobal();

    /// <summary>
    /// Every entry a workspace can name, one per name. An entry in the workspace's own
    /// config wins over a global one of the same name.
    /// </summary>
    IReadOnlyList<EngineRepositorySource> ReadFor(string workspaceRoot);

    /// <summary>
    /// Replaces the global list. A workspace's list is a team file that travels in a
    /// clone, so it is never written from here.
    /// </summary>
    /// <exception cref="Settings.SettingsFileUnreadableException">
    /// The file is there and will not parse, so nothing was written.
    /// </exception>
    void WriteGlobal(IReadOnlyList<EngineRepositorySource> repositories);
}
