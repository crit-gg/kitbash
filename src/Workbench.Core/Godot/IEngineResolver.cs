namespace Workbench.Core.Godot;

/// <summary>
/// Decides which installed engine answers what a workspace asked for.
/// </summary>
/// <remarks>
/// Pure. It is handed the requirement and the list rather than reading either, so the
/// rules can be checked without a disk and a caller that already has the list does not
/// read it twice.
/// </remarks>
public interface IEngineResolver
{
    EngineResolution Resolve(
        EngineRequirement requirement,
        IReadOnlyList<InstalledEngine> installed,
        EngineId? theDefault);
}
