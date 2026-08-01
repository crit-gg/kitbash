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
    /// <remarks>
    /// A non zero exit code is a result rather than an exception, because a program that
    /// reports a problem in its own words is answering the question. Only a program that
    /// could not be started at all throws.
    /// </remarks>
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default);
}
