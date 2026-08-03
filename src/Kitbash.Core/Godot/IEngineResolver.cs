namespace Kitbash.Core.Godot;

/// <summary>
/// Decides which installed engine answers what a workspace asked for.
/// </summary>
public interface IEngineResolver
{
    EngineResolution Resolve(
        EngineRequirement requirement,
        IReadOnlyList<InstalledEngine> installed,
        EngineId? theDefault);
}
