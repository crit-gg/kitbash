namespace Kitbash.Core.Workspaces;

/// <summary>
/// What a <c>kitbash</c> link calls a workspace, which has to be the same on every
/// teammate's machine. Runs git, so call it off the UI thread.
/// </summary>
public interface IWorkspaceLinkNames
{
    /// <summary>
    /// The team name, then the Godot project's, then the repository's first commit. Null
    /// for a folder with none of the three.
    /// </summary>
    Task<string?> ForAsync(string root, CancellationToken cancellation = default);

    /// <summary>The workspaces on the list a link's name means, ignoring case.</summary>
    Task<IReadOnlyList<Workspace>> MatchAsync(string name, CancellationToken cancellation = default);
}
