namespace Workbench.Core.Platform;

/// <summary>
/// One program Workbench runs, and where it was found.
/// </summary>
/// <param name="Name">What was looked for, such as <c>git</c>.</param>
/// <param name="Path">
/// Full path to run, or null when nothing usable was found anywhere.
/// </param>
/// <param name="Configured">The path a person set, or null when they set none.</param>
/// <param name="ConfiguredIsMissing">
/// A path was set and there is nothing runnable there, so PATH answered instead. The
/// program still works if PATH has one, and the person's choice is being ignored, which
/// is worth saying rather than silently doing.
/// </param>
public sealed record ExternalTool(
    string Name,
    string? Path,
    string? Configured,
    bool ConfiguredIsMissing)
{
    public bool IsAvailable => Path is not null;
}

/// <summary>
/// Where the programs Workbench runs are on this machine. A person's override first,
/// then PATH, which is what the app did before the setting existed.
/// </summary>
/// <remarks>
/// Resolved once and held. A person who installs git, or points this at a different one,
/// restarts the app, which is what the setting's own description says and what the app
/// already asked of them for an install. The alternative is searching PATH on every
/// status refresh.
/// </remarks>
public interface IExternalTools
{
    ExternalTool Git { get; }

    ExternalTool Dotnet { get; }
}
