namespace Workbench.Core.Godot;

/// <summary>
/// Thrown when the catalogue can answer neither from the network nor from a cache.
/// </summary>
/// <remarks>
/// A network failure alone is not this. That answers from the cached copy and says it is
/// stale, which is what <see cref="EngineReleases.IsStale"/> is for. This is the case where
/// there is nothing to fall back to.
/// </remarks>
public sealed class EngineCatalogueException : Exception
{
    public EngineCatalogueException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
