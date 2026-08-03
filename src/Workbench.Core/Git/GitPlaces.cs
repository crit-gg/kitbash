namespace Workbench.Core.Git;

/// <summary>
/// The folders one repository keeps itself in. Asked of git, never guessed. A workspace
/// can sit below the repository root, a worktree and a submodule leave a file where the
/// folder would be, and GIT_DIR can point elsewhere again.
/// </summary>
/// <param name="GitDirectory">
/// This working tree's own directory, from <c>rev-parse --absolute-git-dir</c>. Holds
/// <c>HEAD</c>, <c>index</c>, <c>FETCH_HEAD</c> and the other files a command writes as it
/// runs.
/// </param>
/// <param name="CommonDirectory">
/// The directory shared with every other working tree of the same repository, from
/// <c>rev-parse --git-common-dir</c>. Holds the refs and the objects. The same as
/// <see cref="GitDirectory"/> unless this is a worktree.
/// </param>
/// <param name="ReftableDirectory">
/// The reftable stack, or null when this repository stores refs as files. Git 2.45 added
/// this second ref backend. A repository using it keeps every ref here and leaves
/// <c>HEAD</c> a stub that never changes, so watching the git directory alone sees nothing.
/// </param>
public sealed record GitPlaces(
    string GitDirectory,
    string CommonDirectory,
    string? ReftableDirectory)
{
    /// <summary>
    /// Every folder worth watching, without repeats. Keep this list short. Recursing costs
    /// a kernel handle per directory and a fetch writes thousands of files.
    /// </summary>
    public IReadOnlyList<string> Watchable
    {
        get
        {
            var places = new List<string>(3) { GitDirectory };

            if (!string.Equals(CommonDirectory, GitDirectory, StringComparison.Ordinal))
            {
                places.Add(CommonDirectory);
            }

            if (ReftableDirectory is { Length: > 0 } reftable)
            {
                places.Add(reftable);
            }

            return places;
        }
    }
}
