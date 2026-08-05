namespace Kitbash.Core.Git;

/// <summary>
/// One branch, local or remote tracking.
/// </summary>
/// <param name="Name">The short name, such as <c>main</c> or <c>origin/main</c>.</param>
/// <param name="FullName">The whole ref, such as <c>refs/heads/main</c>.</param>
/// <param name="Tip">The commit it points at.</param>
/// <param name="Upstream">The short name of what it tracks, or null when it tracks nothing.</param>
/// <param name="UpstreamIsGone">True when it tracks something the remote no longer has.</param>
/// <param name="Ahead">Commits this branch has that its upstream does not.</param>
/// <param name="Behind">Commits its upstream has that this branch does not.</param>
public sealed record GitBranch(
    string Name,
    string FullName,
    bool IsRemote,
    bool IsCurrent,
    string Tip,
    string? Upstream,
    bool UpstreamIsGone,
    int Ahead,
    int Behind,
    DateTimeOffset? LastCommit,
    string Subject);
