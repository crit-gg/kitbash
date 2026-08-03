namespace Kitbash.Core.Godot;

/// <summary>
/// What Kitbash reads out of a <c>project.godot</c>.
/// </summary>
public sealed record GodotProject
{
    /// <summary>The <c>project.godot</c> itself.</summary>
    public required string File { get; init; }

    /// <summary>The folder holding it, which is what Godot is pointed at.</summary>
    public required string Directory { get; init; }

    /// <summary><c>config/name</c>, or empty when the project does not set one.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// <c>config_version</c>. Godot 3 writes 4 and Godot 4 writes 5. Zero when the file
    /// does not say, which no editor written file does.
    /// </summary>
    public int ConfigVersion { get; init; }

    /// <summary>
    /// The version from <c>config/features</c>, or null when it names none.
    /// </summary>
    public EngineVersionPattern? Version { get; init; }

    /// <summary>Whether this project needs a .NET engine.</summary>
    public bool UsesDotnet { get; init; }

    /// <summary>
    /// False for a Godot 3 project, which Kitbash does not install engines for. Read
    /// as at least 4 rather than exactly 5, since a later Godot may raise it again.
    /// </summary>
    public bool IsSupported => ConfigVersion >= 5;
}
