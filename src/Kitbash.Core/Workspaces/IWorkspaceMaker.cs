namespace Kitbash.Core.Workspaces;

/// <summary>
/// Makes a workspace from nothing: the folder, the Godot project inside it, the
/// repository around it, and the entry in the list.
/// </summary>
public interface IWorkspaceMaker
{
    /// <summary>
    /// Whether a request would work, and what is wrong with it when it would not. Reads
    /// directories, so keep it off the UI thread.
    /// </summary>
    NewWorkspaceState Check(NewWorkspace request);

    /// <summary>
    /// The folder a request would produce, as an absolute path. Empty when the path is
    /// not one that can be resolved.
    /// </summary>
    string Resolve(string path);

    /// <summary>
    /// The folder name a request would produce from a name, with the characters Godot
    /// refuses replaced. Empty when nothing usable is left.
    /// </summary>
    string FolderNameFor(string name);

    /// <summary>
    /// Makes it, and opens it. Everything is written before the workspace joins the list,
    /// so a failure part way leaves nothing in it.
    /// </summary>
    /// <exception cref="IOException">A folder or a file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">A folder or a file could not be written.</exception>
    /// <exception cref="NestedWorkspaceException">
    /// The folder sits inside a workspace that is already in the list.
    /// </exception>
    Task<Workspace> MakeAsync(NewWorkspace request, CancellationToken cancellation = default);
}
