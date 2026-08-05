namespace Kitbash.Core.Git;

/// <summary>
/// Stages and unstages by running git, and by handing it a patch for anything smaller than
/// a whole file.
/// </summary>
public sealed class GitStager : IGitStager
{
    private readonly IGitRunner _git;
    private readonly GitPatchWriter _patches;

    public GitStager(IGitRunner git, GitPatchWriter patches)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(patches);

        _git = git;
        _patches = patches;
    }

    public Task<GitResult> StageAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
        Over(root, cancellation, paths, "add", "--all");

    public Task<GitResult> StageAllAsync(string root, CancellationToken cancellation = default) =>
        _git.RunAsync(root, GitCommand.Of("add", "--all", "--", "."), cancellation);

    // reset rather than restore --staged, which needs a HEAD it can resolve and so fails
    // outright on a repository that has no commits yet.
    public Task<GitResult> UnstageAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
        Over(root, cancellation, paths, "reset", "--quiet");

    public Task<GitResult> DiscardAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
        Over(root, cancellation, paths, "restore", "--worktree");

    // The one that takes its paths as arguments, since git clean has no pathspec file.
    public Task<GitResult> DeleteUntrackedAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
        Run(root, cancellation, ["clean", "--force", "-d", "--"], paths);

    public Task<GitResult> BeginTrackingAsync(
        string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
        Over(root, cancellation, paths, "add", "--intent-to-add");

    public Task<GitResult> StageHunksAsync(
        string root,
        GitPatch patch,
        IReadOnlyCollection<int> hunks,
        CancellationToken cancellation = default) =>
        Apply(root, patch, hunks, reverse: false, cancellation);

    public Task<GitResult> UnstageHunksAsync(
        string root,
        GitPatch patch,
        IReadOnlyCollection<int> hunks,
        CancellationToken cancellation = default) =>
        Apply(root, patch, hunks, reverse: true, cancellation);

    private Task<GitResult> Apply(
        string root,
        GitPatch patch,
        IReadOnlyCollection<int> hunks,
        bool reverse,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(hunks);

        var text = _patches.Write(patch, hunks, reverse);

        if (text.Length == 0)
        {
            return Task.FromResult(GitResult.Skipped);
        }

        var arguments = new List<string>
        {
            "apply",

            // The index alone. The working tree already holds what this is moving.
            "--cached",

            // A patch built here is git's own output with hunks removed, so a warning about
            // whitespace would be about code the person did not write in this gesture.
            "--whitespace=nowarn",
        };

        if (reverse)
        {
            arguments.Add("--reverse");
        }

        // The patch goes in through git's input, since a file can be larger than any
        // operating system allows on a command line.
        arguments.Add("-");

        return _git.RunAsync(root, new GitCommand(arguments).Reading(text), cancellation);
    }

    /// <summary>
    /// Runs a subcommand over paths handed in through git's input, so a selection of any
    /// size works.
    /// </summary>
    private Task<GitResult> Over(
        string root,
        CancellationToken cancellation,
        IReadOnlyList<string> paths,
        params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.Count == 0
            ? Task.FromResult(GitResult.Skipped)
            : _git.RunAsync(root, GitCommand.Of(arguments).Over(paths), cancellation);
    }

    private Task<GitResult> Run(
        string root,
        CancellationToken cancellation,
        List<string> arguments,
        IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0)
        {
            return Task.FromResult(GitResult.Skipped);
        }

        arguments.AddRange(paths);

        return _git.RunAsync(root, new GitCommand(arguments), cancellation);
    }
}
