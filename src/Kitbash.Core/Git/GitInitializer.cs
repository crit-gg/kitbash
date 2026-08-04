using Kitbash.Core.Platform;

namespace Kitbash.Core.Git;

/// <summary>
/// Runs <c>git init</c>. The person's own git decides the default branch name and the ref
/// backend, since both are their configuration and neither is ours to pick.
/// </summary>
public sealed class GitInitializer : IGitInitializer
{
    /// <summary>A backstop. Nothing here reaches a network, so this is only a hang.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly GitEnvironment _environment;

    public GitInitializer(IProcessRunner processes, IExternalTools tools, GitEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(environment);

        _processes = processes;
        _tools = tools;
        _environment = environment;
    }

    public async Task<bool> InitialiseAsync(string directory, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (_tools.Git.Path is not { } git)
        {
            return false;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(Limit);

        try
        {
            var output = await _processes.ReadAsync(
                ProcessRequest
                    .CommandIn(directory, git, "init")
                    .With(_environment.Unattended),
                limit.Token).ConfigureAwait(false);

            return output.Succeeded;
        }
        catch (Exception exception) when (exception is OperationCanceledException or ProcessStartException)
        {
            // A workspace with no repository still works, so a git that would not run is
            // reported rather than thrown and the folder is left as it is.
            return false;
        }
    }
}
