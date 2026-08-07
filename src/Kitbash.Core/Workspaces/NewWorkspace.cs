using Kitbash.Core.Godot;

namespace Kitbash.Core.Workspaces;

/// <summary>
/// One workspace about to be made. Held whole until it is created, so cancelling leaves
/// no folder and no repository behind.
/// </summary>
public sealed record NewWorkspace
{
    /// <summary>What the workspace is called. It also names the folder while
    /// <see cref="CreatesFolder"/> is on.</summary>
    public required string Name { get; init; }

    /// <summary>The folder the workspace will be, not the folder above it.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// The last segment of the path does not exist yet and is created. Off means the path
    /// names a folder that is already there.
    /// </summary>
    public bool CreatesFolder { get; init; } = true;

    /// <summary>False makes an empty workspace, which is a folder and nothing else.</summary>
    public bool HasGodotProject { get; init; }

    /// <summary>
    /// The engine the project is made for, and the build the workspace is pinned to. The
    /// .NET flag is part of it, so a workspace made with a mono engine asks for one later.
    /// Null when no project is being made.
    /// </summary>
    public EngineId? Engine { get; init; }

    public GodotRenderer Renderer { get; init; }

    /// <summary>A git repository is initialised in the folder.</summary>
    public bool UsesGit { get; init; } = true;
}
