namespace Workbench.Core;

/// <summary>
/// Opening a tool. What that means is up to the implementation: most tools will
/// start another application, but some will run a script and some will open a web
/// page, so the launcher asks the tool to activate itself rather than deciding how.
/// </summary>
public interface IToolActivation
{
    ValueTask<ToolActivationResult> ActivateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of an activation. <see cref="Message"/> is shown to the user as is when
/// present, so it should read as a sentence rather than a diagnostic.
/// </summary>
public sealed record ToolActivationResult(bool Succeeded, string? Message = null)
{
    public static ToolActivationResult Success(string? message = null) => new(true, message);

    public static ToolActivationResult Failure(string message) => new(false, message);
}
