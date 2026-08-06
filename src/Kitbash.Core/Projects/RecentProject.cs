namespace Kitbash.Core.Projects;

/// <summary>
/// One entry on the list of things this app has opened. Only the path, the name and when
/// it was last opened are stored. What makes the path a project, and everything else a
/// row says about it, belongs to the app rather than to the store.
/// </summary>
/// <param name="Path">The full path, as the app gave it.</param>
/// <param name="Name">What the app called it when it was remembered.</param>
/// <param name="LastOpened">Null when the stored stamp could not be read.</param>
/// <param name="Exists">Whether a file or a directory is there now.</param>
public sealed record RecentProject(
    string Path,
    string Name,
    DateTimeOffset? LastOpened,
    bool Exists)
{
    /// <summary>The path is gone. An entry is kept rather than dropped when that happens.</summary>
    public bool IsMissing => !Exists;
}
