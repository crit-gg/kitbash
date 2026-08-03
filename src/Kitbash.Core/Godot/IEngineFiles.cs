namespace Kitbash.Core.Godot;

/// <summary>
/// What an engine on disk looks like on this machine. The only part of engine handling
/// that differs per OS, and it is smaller than it looks: naming is not here, because a
/// release manifest names the files and this app never derives one.
/// </summary>
public interface IEngineFiles
{
    /// <summary>The platform this machine runs, which is the only one the page offers.</summary>
    EnginePlatform Platform { get; }

    /// <summary>
    /// The processor this machine runs.
    /// </summary>
    EngineArchitecture Architecture { get; }

    /// <summary>
    /// The editor in an extracted tree, or null when there is nothing that looks like one.
    /// </summary>
    string? FindEditor(string directory);

    /// <summary>
    /// Hides a file from a person browsing the folder.
    /// </summary>
    void Hide(string path);
}
