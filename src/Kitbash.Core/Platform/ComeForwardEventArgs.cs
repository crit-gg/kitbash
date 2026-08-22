namespace Kitbash.Core.Platform;

/// <summary>
/// What a second copy sent when it asked the running one to come forward.
/// </summary>
public sealed class ComeForwardEventArgs : EventArgs
{
    public ComeForwardEventArgs(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
    }

    /// <summary>
    /// Empty when the second copy sent nothing, which is an ordinary launch. A copy
    /// running an older build sends one byte, so anything here is checked before it is
    /// acted on.
    /// </summary>
    public string Message { get; }
}
