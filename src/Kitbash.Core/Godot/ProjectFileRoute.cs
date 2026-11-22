namespace Kitbash.Core.Godot;

/// <summary>Where a <c>project.godot</c> opened from the desktop goes.</summary>
public enum ProjectFileRouteKind
{
    /// <summary>The file is not there.</summary>
    Gone,

    /// <summary>A workspace opens this project, so it goes the way that workspace does.</summary>
    Workspace,

    /// <summary>An installed engine is the one the project asks for.</summary>
    Engine,

    /// <summary>A Godot 3 project, which Kitbash does not match engines for.</summary>
    Unsupported,

    /// <summary>The project names no Godot version.</summary>
    NoVersion,

    /// <summary>Nothing installed is the version and runtime the project asks for.</summary>
    NotInstalled,
}

/// <summary>What <see cref="IProjectFileRouter"/> decided, and what it needs to act on it.</summary>
public sealed record ProjectFileRoute(ProjectFileRouteKind Kind)
{
    /// <summary>Set for <see cref="ProjectFileRouteKind.Workspace"/>.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Set for <see cref="ProjectFileRouteKind.Engine"/>.</summary>
    public InstalledEngine? Engine { get; init; }

    /// <summary>Set whenever the file was read.</summary>
    public GodotProject? Project { get; init; }

    /// <summary>Set for <see cref="ProjectFileRouteKind.Engine"/> and <see cref="ProjectFileRouteKind.NotInstalled"/>.</summary>
    public EngineRequirement? Requirement { get; init; }
}
