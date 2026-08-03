namespace Kitbash.Core.Godot;

/// <summary>
/// What engine a workspace asks for. Reads the workspace's own config first and the
/// Godot project second.
/// </summary>
public interface IEngineRequirementReader
{
    /// <summary>
    /// Touches a disk, so keep it off the UI thread. A workspace that is not there, holds
    /// no Godot project or names no version answers <see cref="EngineRequirement.None"/>
    /// rather than failing.
    /// </summary>
    EngineRequirement Read(string workspaceRoot);
}
