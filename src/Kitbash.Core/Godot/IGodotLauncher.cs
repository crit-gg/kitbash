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
    /// <param name="skipBuild">True opens with whatever assemblies the last build left.</param>
    /// <exception cref="GodotLaunchException">A step failed. It says which.</exception>
    Task OpenAsync(
        InstalledEngine engine,
        GodotProject project,
        GodotLaunchMode mode,
        bool skipBuild,
        IProgress<GodotLaunchStep> progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether opening this project in this engine builds its C# first. Reads settings, so
    /// call it off the UI thread.
    /// </summary>
    bool Builds(InstalledEngine engine, GodotProject project);
}
