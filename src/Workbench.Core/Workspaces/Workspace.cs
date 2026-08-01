namespace Workbench.Core.Workspaces;

/// <summary>
/// A workspace as the launcher shows it. Only <see cref="Root"/> is stored. The name
/// and the states are worked out from disk every time the list is refreshed, so
/// renaming a project or losing a folder shows up without anyone editing a list.
/// </summary>
public sealed record Workspace(string Root, string Name, bool Exists, bool HasRepository)
{
    /// <summary>A workspace with no repository, so there is no branch or history to show.</summary>
    public bool IsLocal => Exists && !HasRepository;

    /// <summary>The folder is gone. Registered workspaces are kept rather than dropped.</summary>
    public bool IsMissing => !Exists;
}
