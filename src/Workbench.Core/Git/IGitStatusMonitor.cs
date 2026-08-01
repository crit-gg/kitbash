namespace Workbench.Core.Git;

/// <summary>
/// Keeps one repository's status current, whoever changed it.
/// </summary>
/// <remarks>
/// The point of this over reading once is that git is used from outside the app more than
/// inside it. A commit from a terminal, a branch switch from an editor, a merge from a
/// tool: all of them have to show up here without anyone pressing anything.
/// </remarks>
public interface IGitStatusMonitor : IDisposable
{
    /// <summary>The last status read, or null before the first read and when the folder is not a repository.</summary>
    GitStatus? Status { get; }

    /// <summary>
    /// Whether the app is in front of the person. False parks the beat and every watch
    /// driven read, so a launcher left open behind other windows costs nothing. Setting it
    /// back to true reads at once, since the time it was away is exactly when something is
    /// most likely to have changed.
    /// </summary>
    /// <remarks>
    /// Starts true, so a caller that never sets it gets a monitor that always runs.
    /// </remarks>
    bool IsActive { get; set; }

    /// <summary>
    /// Raised after <see cref="Status"/> changes to a different value. Not raised when a
    /// read returns what is already showing, so a caller is not woken by a poll that
    /// found nothing.
    /// </summary>
    /// <remarks>Not on any particular thread. A view has to marshal.</remarks>
    event EventHandler? Changed;

    /// <summary>
    /// Starts following a folder, or stops following anything when given null. Replaces
    /// whatever was being followed, and clears <see cref="Status"/> on the way, so this
    /// never describes a folder it is not following. Following what is already being
    /// followed does nothing at all.
    /// </summary>
    void Follow(string? root);

    /// <summary>Reads now rather than waiting. Ignores both the rate limit and <see cref="IsActive"/>.</summary>
    Task RefreshAsync(CancellationToken cancellation = default);
}
