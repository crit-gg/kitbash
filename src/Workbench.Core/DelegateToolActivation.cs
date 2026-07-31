namespace Workbench.Core;

/// <summary>
/// Adapts a callback to <see cref="IToolActivation"/>. Useful for one off cases and
/// for placeholders; a tool with real launch behavior should implement the interface
/// directly so that behavior has somewhere to live.
/// </summary>
public sealed class DelegateToolActivation : IToolActivation
{
    private readonly Func<CancellationToken, ValueTask<ToolActivationResult>> _activate;

    public DelegateToolActivation(Func<CancellationToken, ValueTask<ToolActivationResult>> activate)
    {
        _activate = activate;
    }

    /// <summary>For activations that complete without doing any IO.</summary>
    public static DelegateToolActivation Sync(Func<ToolActivationResult> activate) =>
        new(_ => new ValueTask<ToolActivationResult>(activate()));

    public ValueTask<ToolActivationResult> ActivateAsync(CancellationToken cancellationToken = default) =>
        _activate(cancellationToken);
}
