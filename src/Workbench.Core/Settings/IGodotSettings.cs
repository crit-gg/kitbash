namespace Workbench.Core.Settings;

/// <summary>
/// The Godot choices that belong to one person on one machine. These live in
/// <see cref="IApplicationSettings"/>, which has no layer, so a workspace can never set
/// them for everyone. A path is right for one machine and wrong for every other.
/// </summary>
public interface IGodotSettings
{
    /// <summary>
    /// Where engines Workbench installs are kept. Never blank, since the default is the
    /// engines folder under the data directory rather than nothing. Whether it exists is
    /// checked when something writes there, because a directory can go missing after this
    /// is read.
    /// </summary>
    string EngineDirectory { get; }

    /// <summary>Writes <see cref="EngineDirectory"/>. Engines already installed do not move.</summary>
    void SetEngineDirectory(string value);

    /// <summary>
    /// The install used when a project pins no version, or null when this machine has
    /// none. A name that no longer matches an install reads as none, since an engine can
    /// be uninstalled from under it.
    /// </summary>
    Workbench.Core.Godot.EngineId? DefaultEngine { get; }

    /// <summary>Writes the default. Null clears it, which is what uninstalling it does.</summary>
    void SetDefaultEngine(Workbench.Core.Godot.EngineId? value);
}
