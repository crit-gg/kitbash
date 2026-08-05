namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// A program a person can open the workspace in. This is not <see cref="ExternalTool"/>,
/// which says where git and dotnet are.
/// </summary>
/// <param name="Id">Stable and unique, such as <c>vscode</c> or <c>jetbrains.RD</c>.</param>
/// <param name="Name">What the menu says, such as <c>Visual Studio Code</c>.</param>
/// <param name="IconKey">
/// Names the brand mark, such as <c>vscode</c> or <c>jetbrains/RD</c>. It is a plain string
/// because this library draws nothing, so the application turns it into an asset.
/// </param>
/// <param name="Program">Full path to run.</param>
public sealed record WorkspaceOpener(
    string Id,
    string Name,
    string IconKey,
    string Program,
    WorkspaceOpenerKind Kind)
{
    /// <summary>What a person wrote, before {workspace} is filled in. Null when detected.</summary>
    public string? ArgumentTemplate { get; init; }

    /// <summary>
    /// Arguments before the folder, such as a terminal's own working directory flag.
    /// </summary>
    public IReadOnlyList<string> FixedArguments { get; init; } = [];

    /// <summary>
    /// Extensions this program opens as a project, each with its leading dot. Empty means
    /// the folder is the only thing offered.
    /// </summary>
    public IReadOnlyList<string> OpensFiles { get; init; } = [];

    /// <summary>How far below the workspace to look for those. One is the root alone.</summary>
    public int SearchDepth { get; init; } = 1;

    /// <summary>
    /// Whether the workspace goes in an argument. False for a terminal, which is told
    /// where it is by the working directory, and would read a path as a command to run.
    /// </summary>
    public bool TakesPathArgument { get; init; } = true;
}
