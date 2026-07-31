namespace Workbench.Core;

/// <summary>
/// Wraps a callback as an <see cref="IToolActivation"/>. Intended for placeholders
/// and simple cases. A tool with real launch behavior should implement the interface
/// so that behavior has somewhere to live.
/// </summary>
public sealed class DelegateToolActivation : IToolActivation
{
    private readonly Func<CancellationToken, ValueTask<ToolActivationResult>> _activate;

    public DelegateToolActivation(Func<CancellationToken, ValueTask<ToolActivationResult>> activate)
    {
        _activate = activate;
    }

    /// <summary>For activations that do no IO.</summary>
    public static DelegateToolActivation Sync(Func<ToolActivationResult> activate) =>
        new(_ => new ValueTask<ToolActivationResult>(activate()));

    public ValueTask<ToolActivationResult> ActivateAsync(CancellationToken cancellationToken = default) =>
        _activate(cancellationToken);
}
