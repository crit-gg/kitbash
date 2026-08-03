namespace Workbench.Core.Godot;

/// <summary>What opening a project is doing right now.</summary>
public enum GodotLaunchStage
{
    /// <summary>Working out what has to happen before the editor can open.</summary>
    Checking,

    /// <summary>Deleting the import cache, which only a rebuild does.</summary>
    Cleaning,

    /// <summary>Building the project's C#, so the editor opens with its assemblies ready.</summary>
    Building,

    /// <summary>
    /// Reading the project to find what has changed. Godot's own first pass, and the one
    /// before the import proper.
    /// </summary>
    Scanning,

    /// <summary>Importing assets, which a fresh clone always needs.</summary>
    Importing,

    /// <summary>Starting the editor. Nothing waits on this.</summary>
    Starting,
}

/// <param name="Stage">What is happening.</param>
/// <param name="Detail">
/// A line under it, such as which program is building or how far through a count is.
/// Empty when the stage says enough.
/// </param>
/// <param name="Fraction">
/// How far through, from 0 to 1, when the step knows. Null means it does not, and the
/// bar stays indeterminate rather than inventing a number.
/// </param>
public sealed record GodotLaunchStep(
    GodotLaunchStage Stage, string Detail = "", double? Fraction = null);

/// <summary>A step of opening a project failed.</summary>
public sealed class GodotLaunchException : Exception
{
    public GodotLaunchException(GodotLaunchStage stage, string message, string output)
        : base(message)
    {
        Stage = stage;
        Output = output ?? string.Empty;
    }

    public GodotLaunchException(GodotLaunchStage stage, string message, Exception inner)
        : base(message, inner)
    {
        Stage = stage;
        Output = string.Empty;
    }

    public GodotLaunchStage Stage { get; }

    /// <summary>What the program wrote, both pipes together. Empty when it never ran.</summary>
    public string Output { get; }
}
