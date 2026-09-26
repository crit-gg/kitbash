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

    /// <summary>
    /// What <c>godot.repository</c> says, or null when it says nothing and official builds
    /// are wanted. Set even when no list holds that name.
    /// </summary>
    public string? RepositoryName { get; init; }

    /// <summary>The entry that name found, or null when it found none.</summary>
    public EngineRepositorySource? Repository { get; init; }

    /// <summary>True when a repository was named and no list holds it, so nothing can be installed.</summary>
    public bool IsRepositoryUnknown => RepositoryName is not null && Repository is null;

    /// <summary>
    /// The install slot a newest pin owns, such as <c>4.7.2-mono</c>, keyed by what was
    /// pinned. Null for official builds and for a pin naming one build.
    /// </summary>
    public string? Slot
    {
        get
        {
            if (Repository is null || Version is not { IsExact: false } version)
            {
                return null;
            }

            var text = version.ToString();

            if (text.EndsWith(MonoSuffix, StringComparison.Ordinal))
            {
                text = text[..^MonoSuffix.Length];
            }

            return NeedsDotnet ? text + MonoSuffix : text;
        }
    }

    private const string MonoSuffix = "-mono";
}
