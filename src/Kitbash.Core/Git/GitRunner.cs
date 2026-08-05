using System.Text;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Git;

/// <summary>
/// Runs git inside one repository, with the flags and the encoding every run needs.
/// </summary>
public sealed class GitRunner : IGitRunner
{
    /// <summary>
    /// Git speaks UTF 8 whatever the machine. Without this the console encoding decodes it,
    /// which on Windows is the OEM code page and turns any name outside ASCII to nonsense.
    /// </summary>
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly IFileSystem _fileSystem;
    private readonly GitEnvironment _environment;

    public GitRunner(
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

    public bool IsAvailable => _tools.Git.Path is not null;

    public async Task<GitResult> RunAsync(
        string root, GitCommand command, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(command);

        if (_tools.Git.Path is not { } git || !_fileSystem.DirectoryExists(root))
        {
            return GitResult.Nothing(GitRunOutcome.Unavailable);
        }

        var request = new ProcessRequest(git, Arguments(command), UseShellExecute: false, root)
        {
            StandardInput = command.StandardInput,
            TextEncoding = Utf8,
            Environment = command.Network ? _environment.Unattended : null,
        };

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(command.Limit);

        try
        {
            var output = await _processes.ReadAsync(request, limit.Token).ConfigureAwait(false);

            return new GitResult(
                GitRunOutcome.Ran, output.ExitCode, output.StandardOutput, output.StandardError);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return GitResult.Nothing(GitRunOutcome.TimedOut);
        }
        catch (ProcessStartException)
        {
            return GitResult.Nothing(GitRunOutcome.DidNotStart);
        }
    }

    /// <summary>
    /// The command, behind the three settings every run wants. All of them come before the
    /// subcommand, which is where git reads its own options.
    /// </summary>
    private static List<string> Arguments(GitCommand command)
    {
        var arguments = new List<string>(command.Arguments.Count + 4)
        {
            // Plain reads otherwise write the index back to refresh its stat cache, and
            // anything watching the git directory then sees its own read as a change.
            "--no-optional-locks",

            // A configured pager would take the output away and wait for a key.
            "--no-pager",

            // Off, a path outside ASCII comes back with its bytes written as escapes.
            "-c",
            "core.quotepath=false",
        };

        arguments.AddRange(command.Arguments);

        return arguments;
    }
}
