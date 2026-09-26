namespace Kitbash.Core.Godot;

/// <summary>Which hash a source publishes. Each value's text is what an install record keeps.</summary>
public enum EngineChecksumKind
{
    /// <summary>What the Godot project publishes in every release manifest.</summary>
    Sha512,

    /// <summary>What GitHub states for every release asset.</summary>
    Sha256,
}
