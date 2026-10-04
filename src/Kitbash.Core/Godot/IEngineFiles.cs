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
    /// The program a terminal should run for an editor, which can be a sibling of it.
    /// </summary>
    string CommandFor(string editor);

    /// <summary>
    /// Hides a file from a person browsing the folder.
    /// </summary>
    void Hide(string path);

    /// <summary>
    /// Where Godot looks for export templates, one folder per version under it. Godot's own
    /// data directory, which is not Kitbash's, and which an engine in self contained mode
    /// would not read.
    /// </summary>
    string ExportTemplatesDirectory { get; }
}
