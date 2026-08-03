namespace Kitbash.Core.Godot;

/// <summary>
/// Thrown when the catalogue can answer neither from the network nor from a cache.
/// </summary>
public sealed class EngineCatalogueException : Exception
{
    public EngineCatalogueException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
