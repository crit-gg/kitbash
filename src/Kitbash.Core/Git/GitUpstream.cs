namespace Kitbash.Core.Git;

/// <summary>
/// What a branch tracks. Git records it as a remote and a ref on that remote, and neither
/// has to match the local branch's name.
/// </summary>
/// <param name="Remote">The remote's name, or a single dot when the upstream is local.</param>
/// <param name="Branch">What the branch is called on that remote.</param>
/// <param name="IsGone">True when the remote no longer has it, so the tracking ref is missing.</param>
public sealed record GitUpstream(string Remote, string Branch, bool IsGone = false)
{
    /// <summary>Git spells a local upstream with a dot where a remote's name would be.</summary>
    public bool IsLocal => Remote == ".";

    /// <summary>The short name that resolves here, such as <c>origin/main</c>.</summary>
    public string Ref => IsLocal ? Branch : $"{Remote}/{Branch}";
}
