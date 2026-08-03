namespace Workbench.Core.Godot;

/// <summary>
/// Opens a project in an engine, doing whatever has to happen first.
/// </summary>
public interface IGodotLauncher
{
    /// <summary>
    /// Builds the project's C# if it needs it, imports its assets if they are not there,
    /// then starts it detached and returns without waiting for it.
    /// </summary>
    /// <remarks>
    /// Runs processes and walks a project, so never on the UI thread. Cancelling stops
    /// the step that is running and its process tree with it. What has already been built
    /// or imported stays, since both are caches and neither is worth undoing.
    /// </remarks>
    /// <exception cref="GodotLaunchException">A step failed. It says which.</exception>
    Task OpenAsync(
        InstalledEngine engine,
        GodotProject project,
        GodotLaunchMode mode,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken);
}
