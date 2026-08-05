namespace Kitbash.Core.Git;

/// <summary>Commits by running git, with the message handed over through its input.</summary>
public sealed class GitCommitter : IGitCommitter
{
    private readonly IGitRunner _git;

    public GitCommitter(IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public Task<GitResult> CommitAsync(
        string root,
        string message,
        GitCommitOptions? options = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options ?? GitCommitOptions.Default;

        var arguments = new List<string>
        {
            "commit",

            // The message comes in through git's input rather than an argument, so a long
            // one cannot run into the command line limit and nothing needs quoting.
            "--file=-",

            // Off, git strips comment lines out of the message, and a line starting with a
            // hash is ordinary text in one a person typed into a box.
            "--cleanup=whitespace",
        };

        if (settings.Amend)
        {
            arguments.Add("--amend");
        }

        if (settings.AllowEmpty)
        {
            arguments.Add("--allow-empty");
        }

        if (settings.StageTracked)
        {
            arguments.Add("--all");
        }

        if (settings.SignOff)
        {
            arguments.Add("--signoff");
        }

        if (settings.Author is { Length: > 0 } author)
        {
            arguments.Add("--author=" + author);
        }

        return _git.RunAsync(root, new GitCommand(arguments).Reading(message), cancellation);
    }

    public async Task<GitIdentity?> ReadIdentityAsync(
        string root, CancellationToken cancellation = default)
    {
        var name = await ReadAsync(root, "user.name", cancellation).ConfigureAwait(false);
        var email = await ReadAsync(root, "user.email", cancellation).ConfigureAwait(false);

        return name is { Length: > 0 } && email is { Length: > 0 }
            ? new GitIdentity(name, email)
            : null;
    }

    private async Task<string?> ReadAsync(string root, string key, CancellationToken cancellation)
    {
        var result = await _git
            .RunAsync(root, GitCommand.Of("config", "--get", key), cancellation)
            .ConfigureAwait(false);

        return result.Succeeded ? result.Output.Trim() : null;
    }
}
