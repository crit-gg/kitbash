namespace Workbench.Core.Platform;

/// <summary>Starts processes. Replace this to keep tests from launching anything.</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts a program and does not wait. For handing a target to the desktop, where
    /// there is nothing to read back.
    /// </summary>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    void Run(ProcessRequest request);

    /// <summary>
    /// Runs a program to completion and returns what it wrote.
    /// </summary>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default);

    /// <summary>
    /// The same, calling <paramref name="onLine"/> for each line of standard output as it
    /// arrives rather than only at the end.
    /// </summary>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    Task<ProcessOutput> ReadLinesAsync(
        ProcessRequest request, Action<string> onLine, CancellationToken cancellation = default);
}
