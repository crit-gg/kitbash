namespace Workbench.Core.Godot;

/// <summary>
/// Thrown when an engine directory does not hold what it was said to hold.
/// </summary>
public sealed class EngineStoreException : Exception
{
    public EngineStoreException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
