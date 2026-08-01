namespace Workbench.Core.Platform;

/// <summary>Starts processes. Replace this to keep tests from launching anything.</summary>
public interface IProcessRunner
{
    /// <exception cref="ProcessStartException">The process could not be started.</exception>
    void Run(ProcessRequest request);
}
