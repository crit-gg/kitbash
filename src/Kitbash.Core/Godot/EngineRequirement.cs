namespace Kitbash.Core.Godot;

/// <summary>Where a workspace's engine requirement was read from.</summary>
public enum EngineRequirementSource
{
    /// <summary>Nothing names a version. The default install decides.</summary>
    None,

    /// <summary>The workspace's own <c>.kitbash</c> config said so.</summary>
    Workspace,

    /// <summary>Read off <c>config/features</c> in the project's <c>project.godot</c>.</summary>
    Project,
}

/// <summary>
/// The engine a workspace asks for, before anything looks at what is installed.
/// </summary>
public sealed record EngineRequirement
{
    /// <summary>Nothing was asked for, which is what a folder with no Godot project gives.</summary>
    public static EngineRequirement None { get; } = new();

    /// <summary>Null when nothing names a version.</summary>
    public EngineVersionPattern? Version { get; init; }

    /// <summary>True when the project has C# in it, so only a .NET engine will do.</summary>
    public bool NeedsDotnet { get; init; }

    public EngineRequirementSource Source { get; init; } = EngineRequirementSource.None;

    /// <summary>The project the flag was read from, when there is one.</summary>
    public GodotProject? Project { get; init; }

    public bool HasVersion => Version is not null;
}
