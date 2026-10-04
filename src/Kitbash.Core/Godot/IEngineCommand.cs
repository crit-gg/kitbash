namespace Kitbash.Core.Godot;

/// <summary>
/// Keeps <c>godot</c> on PATH running the default engine, while a person has asked for it.
/// </summary>
public interface IEngineCommand
{
    /// <summary>
    /// Makes the command match the settings and says where it stands. Never throws, and
    /// calls wait their turn. Touches a disk and can run a process, so never on the UI thread.
    /// </summary>
    Task<EngineCommandState> SyncAsync(CancellationToken cancellation);
}
