namespace Workbench.ViewModels;

/// <summary>Which engine state the strip is reporting.</summary>
public enum EngineState
{
    /// <summary>The installed engine is the one the project asks for.</summary>
    Matched,

    /// <summary>An engine is selected and it is not the one the project asks for.</summary>
    Mismatch,

    /// <summary>The project asks for an engine that is not installed.</summary>
    Missing,
}

/// <summary>
/// The engine strip above the tool list.
/// </summary>
/// <remarks>
/// <para>
/// **PLACEHOLDER. Every value in <see cref="Placeholder"/> is invented.** It exists so
/// the strip can be laid out and looked at, and it is the only invented data in the app.
/// Delete <see cref="Placeholder"/> when the strip is wired and this class keeps its
/// shape.
/// </para>
/// <para>
/// Two of these fields are already readable and need no new subsystem.
/// <c>project.godot</c> carries <c>config/features=PackedStringArray("4.7", "C#")</c>,
/// which gives <see cref="Version"/> and whether <see cref="Runtime"/> applies.
/// <c>WorkspaceNameResolver</c> already reads that file by line, since its keys contain
/// slashes and it is not TOML.
/// </para>
/// <para>
/// The rest needs engine discovery: which engines are installed, where, and whether one
/// matches. That decides <see cref="State"/>, <see cref="Note"/> and what
/// <see cref="Action"/> can offer, and it is a piece of work of its own.
/// </para>
/// </remarks>
public sealed class EngineViewModel
{
    public required string Version { get; init; }

    /// <summary>The runtime badge, or empty when the project is not a runtime project.</summary>
    public required string Runtime { get; init; }

    public required EngineState State { get; init; }

    /// <summary>What the state means, in words. Empty when there is nothing to add.</summary>
    public required string StateLabel { get; init; }

    /// <summary>The install path, or the reason there is not one.</summary>
    public required string Note { get; init; }

    public required string Action { get; init; }

    public bool HasRuntime => !string.IsNullOrWhiteSpace(Runtime);

    public bool HasState => !string.IsNullOrWhiteSpace(StateLabel);

    public bool IsMatched => State == EngineState.Matched;

    public bool IsMismatch => State == EngineState.Mismatch;

    public bool IsMissing => State == EngineState.Missing;

    public string BadgeTier => State switch
    {
        EngineState.Mismatch => "Modified",
        EngineState.Missing => "Error",
        _ => "Ok",
    };

    /// <summary>Invented. See the remarks on this class, and delete this when it is wired.</summary>
    public static EngineViewModel Placeholder => new()
    {
        Version = "Godot 4.7.1 stable",
        Runtime = ".NET",
        State = EngineState.Matched,
        StateLabel = "",
        Note = "Placeholder. The engine strip is not wired to anything yet.",
        Action = "Open in Godot",
    };
}
