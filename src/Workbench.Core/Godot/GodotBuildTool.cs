namespace Workbench.Core.Godot;

/// <summary>What builds a project's C# before the editor opens.</summary>
/// <remarks>
/// Two programs can do it and neither is always right, so this is a choice rather than
/// a guess. dotnet is faster and says more when a build fails. The editor needs nothing
/// installed beyond the engine already being used.
/// </remarks>
public enum GodotBuildTool
{
    /// <summary>dotnet when this machine has one, the editor otherwise.</summary>
    Auto,

    /// <summary><c>dotnet build</c> on the solution, or the project when there is none.</summary>
    Dotnet,

    /// <summary>The engine itself, through <c>--build-solutions</c>.</summary>
    Editor,

    /// <summary>Nothing. The editor opens with whatever assemblies are already there.</summary>
    None,
}
