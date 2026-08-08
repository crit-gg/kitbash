using Kitbash.Core.Git;

namespace Kitbash.Workspaces;

/// <summary>
/// Says when a repository's head has moved, which is a branch switch or new commits.
/// A workspace's own config is committed to the repository, so a move means it has to be
/// read again.
/// </summary>
public sealed class GitHeadTracker
{
    private (string Branch, string Commit)? _head;

    /// <summary>
    /// Takes the latest status and answers whether the head moved from the one before it.
    /// The first status for a repository is never a move, since it arrives with the load
    /// that asked for it, and neither is losing a repository, since there is nothing to
    /// read a workspace's config for until one is followed again.
    /// </summary>
    public bool Moved(GitStatus? status)
    {
        var head = status is { } read ? (read.Branch, read.Commit) : default((string, string)?);
        var moved = _head is { } was && head is { } now && was != now;

        _head = head;

        return moved;
    }
}
