namespace Kitbash.Core.Workspaces;

/// <summary>Works out what a workspace is called.</summary>
public interface IWorkspaceNameResolver
{
    /// <summary>
    /// In order: this person's own name for it, the name set in the workspace's team
    /// config, the name from the first <c>project.godot</c> found under the folder, then
    /// the folder name.
    /// </summary>
    string Resolve(string root);

    /// <summary>
    /// Names the workspace for this person alone, in the user layer, so nothing a
    /// colleague sees changes. Blank takes the name away and the order above decides again.
    /// </summary>
    /// <exception cref="Settings.SettingsFileUnreadableException">The file is there and could not be read.</exception>
    void SetName(string root, string? name);
}
