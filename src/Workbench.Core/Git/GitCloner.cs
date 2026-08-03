using Workbench.Core.IO;
using Workbench.Core.Platform;

namespace Workbench.Core.Git;

/// <summary>
/// Runs <c>git clone</c> unattended. The one thing in the app that writes a folder a
/// person did not already have.
/// </summary>
public sealed class GitCloner : IGitCloner
{
    /// <summary>A backstop for a link that accepts the connection and then says nothing.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(30);

    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly IFileSystem _fileSystem;
    private readonly GitEnvironment _environment;

    public GitCloner(
        IProcessRunner processes,
        IExternalTools tools,
        IFileSystem fileSystem,
        GitEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environment);

        _processes = processes;
        _tools = tools;
        _fileSystem = fileSystem;
        _environment = environment;
    }

    public async Task<GitCloneResult> CloneAsync(
        GitRemote remote, string parentDirectory, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);

        var parent = Path.GetFullPath(parentDirectory);
        var destination = Path.Combine(parent, remote.Name);

        if (_tools.Git.Path is not { } git)
        {
            return new GitCloneResult(GitCloneOutcome.GitMissing, destination, string.Empty);
        }

        if (_fileSystem.DirectoryExists(destination) || _fileSystem.FileExists(destination))
        {
            return new GitCloneResult(GitCloneOutcome.DestinationExists, destination, string.Empty);
        }

        try
        {
            _fileSystem.CreateDirectory(parent);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GitCloneResult(GitCloneOutcome.Failed, destination, exception.Message);
        }

        var result = await RunAsync(git, parent, destination, remote, cancellation).ConfigureAwait(false);

        // Git leaves what it had written when it is killed or fails part way through, and
        // the destination did not exist a moment ago, so anything there now is that.
        if (!result.Succeeded)
        {
            Discard(destination);
        }

        return result;
    }

    private async Task<GitCloneResult> RunAsync(
        string git,
        string parent,
        string destination,
        GitRemote remote,
        CancellationToken cancellation)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(Limit);

        ProcessOutput output;

        try
        {
            // Two dashes, so an address that begins with a dash is an address and not an
            // option. The parent is the working directory and the folder is named after it.
            output = await _processes.ReadAsync(
                ProcessRequest
                    .CommandIn(parent, git, "clone", "--", remote.Address, remote.Name)
                    .With(_environment.Unattended),
                limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new GitCloneResult(
                cancellation.IsCancellationRequested ? GitCloneOutcome.Cancelled : GitCloneOutcome.TimedOut,
                destination,
                string.Empty);
        }
        catch (ProcessStartException exception)
        {
            return new GitCloneResult(GitCloneOutcome.Failed, destination, exception.Message);
        }

        var message = output.StandardError.Trim();

        return output.Succeeded
            ? new GitCloneResult(GitCloneOutcome.Cloned, destination, message)
            : new GitCloneResult(Read(message), destination, message);
    }

    private void Discard(string destination)
    {
        try
        {
            _fileSystem.DeleteDirectory(destination);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The folder is already reported as a failure. Leaving it is untidy and
            // deleting it was never the thing being asked for.
        }
    }

    // Git says why in words rather than in an exit code, so the words are what there is to
    // read. Anything unrecognised is a plain failure, which is the safe way round.
    private static GitCloneOutcome Read(string message)
    {
        if (message.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not read Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
        {
            return GitCloneOutcome.NeedsCredentials;
        }

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not appear to be a git repository", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Repository not found", StringComparison.OrdinalIgnoreCase))
        {
            return GitCloneOutcome.NotFound;
        }

        return GitCloneOutcome.Failed;
    }
}
