namespace Kitbash.Settings;

/// <summary>
/// Whether the launcher closes after it has started something in Godot. Read again at
/// every launch, so a change applies at once.
/// </summary>
public interface ILauncherCloseSettings
{
    /// <summary>True closes the launcher once a project manager is running.</summary>
    bool AfterProjectManager { get; }

    /// <summary>True closes the launcher once a project is open in the editor.</summary>
    bool AfterEditor { get; }

    /// <summary>True closes the launcher once a project is running.</summary>
    bool AfterPlay { get; }
}
