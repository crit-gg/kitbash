namespace Kitbash.Core.Godot;

/// <summary>
/// Opens a project in an engine, doing whatever has to happen first.
/// </summary>
public interface IGodotLauncher
{
    /// <summary>
    /// Builds the project's C# if it needs it, imports its assets if they are not there,
    /// then starts it detached and returns without waiting for it.
    /// </summary>
    /// <exception cref="GodotLaunchException">A step failed. It says which.</exception>
    Task OpenAsync(
        InstalledEngine engine,
        GodotProject project,
        GodotLaunchMode mode,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken);
}
