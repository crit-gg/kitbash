using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>Decides what opening a <c>project.godot</c> from the desktop does.</summary>
public interface IProjectFileRouter
{
    /// <summary>
    /// A project one of the workspaces opens goes to that workspace. Any other project is
    /// matched against the engines installed here. Touches a disk.
    /// </summary>
    /// <param name="workspaceRoots">The workspaces to look in, whose folders are present.</param>
    Task<ProjectFileRoute> RouteAsync(
        ProjectFileArgument file,
        IReadOnlyList<string> workspaceRoots,
        CancellationToken cancellationToken);
}
