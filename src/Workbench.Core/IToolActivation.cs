namespace Workbench.Core;

/// <summary>
/// Opening a tool. Implementations decide what that means, such as starting an
/// application, running a script, or opening a web page.
/// </summary>
public interface IToolActivation
{
    ValueTask<ToolActivationResult> ActivateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of an activation. <see cref="Message"/> is shown to the user as written.
/// </summary>
public sealed record ToolActivationResult(bool Succeeded, string? Message = null)
{
    public static ToolActivationResult Success(string? message = null) => new(true, message);

    public static ToolActivationResult Failure(string message) => new(false, message);
}
