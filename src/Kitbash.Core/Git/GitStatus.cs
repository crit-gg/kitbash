namespace Kitbash.Core.Git;

/// <summary>
/// What a repository looks like right now.
/// </summary>
/// <param name="Places">Where git keeps this repository's own files.</param>
/// <param name="Branch">The branch name, or the short commit when the head is detached.</param>
/// <param name="IsDetached">True when the head is a commit rather than a branch.</param>
/// <param name="HasUpstream">
/// False when the branch tracks nothing, which is not the same as being level with it.
/// Ahead and behind are both zero either way, so only this tells them apart.
/// </param>
/// <param name="Ahead">Commits the branch has that its upstream does not.</param>
/// <param name="Behind">Commits the upstream has that the branch does not.</param>
/// <param name="Modified">Files changed in the working tree and not staged.</param>
/// <param name="Staged">Files changed in the index.</param>
/// <param name="Conflicted">Files with an unresolved merge.</param>
/// <param name="LastFetch">
/// When the repository last fetched, read from the timestamp git leaves behind. Null when
/// it never has.
/// </param>
public sealed record GitStatus(
    GitPlaces Places,
    string Branch,
    bool IsDetached,
    bool HasUpstream,
    int Ahead,
    int Behind,
    int Modified,
    int Staged,
    int Conflicted,
    DateTimeOffset? LastFetch)
{
    /// <summary>Nothing changed, nothing staged, nothing in conflict.</summary>
    public bool IsClean => Modified == 0 && Staged == 0 && Conflicted == 0;
}
