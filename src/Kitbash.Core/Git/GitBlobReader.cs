namespace Kitbash.Core.Git;

/// <summary>
/// Reads a file out of a revision by running git. The path goes in as one argument, so a
/// name holding a space or a dash needs no quoting and cannot be read as an option.
/// </summary>
public sealed class GitBlobReader : IGitBlobReader
{
    private readonly IGitRunner _git;

    public GitBlobReader(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public async Task<string?> ReadTextAsync(
        string root, string revision, string path, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // An empty revision is the index, which git spells as a bare colon. Both forms are
        // one argument, so nothing here has to quote anything.
        var result = await _git
            .RunAsync(root, GitCommand.Of("show", revision + ":" + path), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? result.Output : null;
    }
}
