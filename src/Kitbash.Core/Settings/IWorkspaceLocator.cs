namespace Kitbash.Core.Settings;

/// <summary>Finds the workspace a directory belongs to.</summary>
public interface IWorkspaceLocator
{
    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a workspace, the way
    /// git finds <c>.git</c>. Returns null when there is none, which is normal.
    /// </summary>
    WorkspacePaths? Discover(string startDirectory);
}
