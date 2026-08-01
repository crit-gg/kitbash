namespace Workbench.Core.Workspaces;

/// <summary>Works out what a workspace is called.</summary>
public interface IWorkspaceNameResolver
{
    /// <summary>
    /// In order: the name set in the workspace's team config, then the name from the
    /// first <c>project.godot</c> found under the folder, then the folder name.
    /// </summary>
    string Resolve(string root);
}
