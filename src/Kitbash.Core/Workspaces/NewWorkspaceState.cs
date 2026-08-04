namespace Kitbash.Core.Workspaces;

/// <summary>
/// What a request to make a workspace amounts to. Two of these allow it and the rest
/// refuse it. The words a person reads belong to the application, not here.
/// </summary>
public enum NewWorkspaceState
{
    /// <summary>The folder is there and it is empty.</summary>
    Ready,

    /// <summary>The folder is not there yet and will be made.</summary>
    WillCreate,

    NameMissing,

    PathMissing,

    /// <summary>The path is relative, so it names no one place.</summary>
    PathNotFull,

    /// <summary>The last segment holds characters Godot refuses in a folder name.</summary>
    FolderNameNotAllowed,

    /// <summary>The folder is already a workspace, or sits inside one.</summary>
    WorkspaceExists,

    /// <summary>The folder above the one being made is not there.</summary>
    ParentMissing,

    FolderMissing,

    /// <summary>The folder holds files, so creating here would mix two projects.</summary>
    FolderNotEmpty,
}
