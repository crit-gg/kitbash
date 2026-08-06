using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>Running a script tool, which the launcher waits for rather than letting go.</summary>
public interface IToolScriptRunner
{
    /// <summary>
    /// Runs the script in its own folder and reports every line it writes. Cancelling kills
    /// it and everything it started.
    /// </summary>
    /// <param name="arguments">What the form answered, already turned into arguments.</param>
    /// <param name="progress">Told about every line, on whichever thread read it.</param>
    /// <exception cref="ProcessStartException">The script could not be started.</exception>
    Task<ToolRunOutcome> RunAsync(
        InstalledTool tool,
        string? workspaceRoot,
        IReadOnlyList<string> arguments,
        IProgress<ToolProgressStep> progress,
        CancellationToken cancellation);
}
