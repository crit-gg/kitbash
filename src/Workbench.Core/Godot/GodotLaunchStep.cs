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

    /// <summary>Importing assets, which a fresh clone always needs.</summary>
    Importing,

    /// <summary>Starting the editor. Nothing waits on this.</summary>
    Starting,
}

/// <param name="Stage">What is happening.</param>
/// <param name="Detail">
/// A line under it, such as which program is building. Empty when the stage says enough.
/// </param>
public sealed record GodotLaunchStep(GodotLaunchStage Stage, string Detail = "");

/// <summary>A step of opening a project failed.</summary>
/// <remarks>
/// Carries what the program wrote, because a build failure is only useful with its
/// output and there is nowhere else a person could go and read it.
/// </remarks>
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
