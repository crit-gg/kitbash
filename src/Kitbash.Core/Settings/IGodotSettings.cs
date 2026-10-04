namespace Kitbash.Core.Settings;

/// <summary>
/// The Godot choices that belong to one person on one machine. These live in
/// <see cref="IApplicationSettings"/>, which has no layer, so a workspace can never set
/// them for everyone. A path is right for one machine and wrong for every other.
/// </summary>
public interface IGodotSettings
{
    /// <summary>
    /// Where engines Kitbash installs are kept. Never blank, since the default is the
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
    Kitbash.Core.Godot.EngineId? DefaultEngine { get; }

    /// <summary>Writes the default. Null clears it, which is what uninstalling it does.</summary>
    void SetDefaultEngine(Kitbash.Core.Godot.EngineId? value);

    /// <summary>Whether the default engine is put on PATH as godot.</summary>
    bool DefaultOnPath { get; }

    /// <summary>
    /// What builds a project's C# before the editor opens.
    /// <see cref="Kitbash.Core.Godot.GodotBuildTool.Auto"/> unless a person said.
    /// </summary>
    Kitbash.Core.Godot.GodotBuildTool BuildTool { get; }
}
