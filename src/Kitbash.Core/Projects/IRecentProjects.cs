namespace Kitbash.Core.Projects;

/// <summary>
/// What this app has opened before, newest first. The store keeps the list and never
/// decides what a project is, so an app hands it a path it has already accepted.
/// </summary>
public interface IRecentProjects
{
    /// <summary>Newest first. A path that has gone is kept and reads as missing.</summary>
    IReadOnlyList<RecentProject> All { get; }

    /// <summary>
    /// Puts a path at the front and stamps it as opened now. Remembering one that is
    /// already on the list moves it rather than adding it twice.
    /// </summary>
    RecentProject Remember(string path, string name);

    /// <summary>Renames an entry without moving it. Does nothing for a path not on the list.</summary>
    void Rename(string path, string name);

    /// <summary>Takes a path off the list. Nothing on disk is touched.</summary>
    void Forget(string path);

    /// <summary>Reads the list and the paths on disk again.</summary>
    void Refresh();
}
