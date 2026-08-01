namespace Workbench.Core.Git;

/// <summary>
/// The folders one repository keeps itself in. Asked of git rather than guessed, because
/// <c>.git</c> under the workspace is right only in the simplest case: a workspace can sit
/// below the repository root, a worktree and a submodule leave a file there instead of a
/// folder, and GIT_DIR can point somewhere else again.
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
/// The reftable stack, or null when this repository stores refs as files. Git 2.45 added a
/// second way to store refs, and a repository created with it keeps every ref in here and
/// leaves <c>HEAD</c> as a stub that never changes. Measured on git 2.55: a branch switch
/// and a branch rename both write nothing outside this folder.
/// </param>
public sealed record GitPlaces(
    string GitDirectory,
    string CommonDirectory,
    string? ReftableDirectory)
{
    /// <summary>
    /// Every folder worth watching, without repeats. Small on purpose. Watching one folder
    /// per ref, or recursing, costs a kernel handle each and drowns in a fetch.
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
