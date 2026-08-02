namespace Workbench.Core.Godot;

/// <summary>
/// Thrown when an engine directory does not hold what it was said to hold.
/// </summary>
/// <remarks>
/// Listing never throws this. A folder that turns out not to be an engine is left out of
/// the list, which is an answer. This is for registering one that has just been unpacked,
/// where the caller stated there is an engine there and there is not.
/// </remarks>
public sealed class EngineStoreException : Exception
{
    public EngineStoreException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
