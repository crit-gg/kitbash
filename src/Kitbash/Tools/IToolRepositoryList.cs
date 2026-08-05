namespace Kitbash.Tools;

/// <summary>
/// The repositories offering tools here. Availability is derived from these rather than
/// stored, so dropping an entry stops a tool being offered without touching what is
/// installed.
/// </summary>
public interface IToolRepositoryList
{
    /// <summary>
    /// The global list first, then the open workspace's, each in the order its file writes
    /// them. That order is what makes a collision refuse the same one twice running.
    /// Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ToolRepositorySource> Read();
}
