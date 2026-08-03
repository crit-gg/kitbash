namespace Kitbash.Core.Workspaces;

/// <summary>
/// Thrown when a folder sits inside a workspace that is already added. Nesting is
/// refused because settings are found by walking up to the nearest <c>.kitbash</c>,
/// so a tool started in the inner folder and one started in the outer folder would
/// disagree about which workspace they are in.
/// </summary>
public sealed class NestedWorkspaceException : Exception
{
    public NestedWorkspaceException(string folder, string containingRoot)
        : base($"'{folder}' is inside the workspace at '{containingRoot}'.")
    {
        Folder = folder;
        ContainingRoot = containingRoot;
    }

    /// <summary>The folder that was refused.</summary>
    public string Folder { get; }

    /// <summary>The workspace it sits inside.</summary>
    public string ContainingRoot { get; }
}
