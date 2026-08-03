using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;
using Workbench.Core.IO;

namespace Workbench.ViewModels;

/// <summary>Which engine state the strip is reporting.</summary>
public enum EngineState
{
    /// <summary>There is nothing to report. No workspace, or no Godot project in it.</summary>
    Quiet,

    /// <summary>The installed engine is the one the project asks for.</summary>
    Matched,

    /// <summary>An engine will be used and it is not the one the project asks for.</summary>
    Mismatch,

    /// <summary>Nothing installed can open this project.</summary>
    Missing,
}

/// <summary>
/// The engine strip above the tool list. What this workspace asks for, what will open it,
/// and how well those two agree.
/// </summary>
/// <remarks>
/// <para>
/// Built whole from an <see cref="EngineResolution"/> and replaced rather than updated, so
/// there is no half applied state and nothing to keep in step. Every rule about which
/// engine answers a project lives in <see cref="IEngineResolver"/> and none of it is here.
/// This turns one answer into words.
/// </para>
/// <para>
/// **The version shown is the engine that would open the project, not what was asked
/// for**, except when there is no engine to name and the request is all there is to show.
/// A strip reading 4.7 while 4.6 is what opens would be worse than saying nothing.
/// </para>
/// </remarks>
public sealed partial class EngineViewModel : ViewModelBase
{
    /// <summary>Roughly what fits the strip's second line at the launcher's width.</summary>
    private const int NoteLength = 58;

    private readonly LauncherViewModel? _launcher;

    private EngineViewModel()
    {
    }

    public EngineViewModel(
        EngineResolution resolution,
        IPathShortener paths,
        LauncherViewModel launcher)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(launcher);

        _launcher = launcher;
        Resolution = resolution;

        var requirement = resolution.Requirement;
        var asked = requirement.Version?.ToString();

        // The badge sits beside the engine's name, so it describes the engine whenever
        // one is named and the project only when none is. They agree in every case but
        // one, and that one is a .NET project about to open in a plain engine, where a
        // .NET badge next to that engine's version says the opposite of what is wrong.
        Runtime = (resolution.Engine?.IsMono ?? requirement.NeedsDotnet) ? ".NET" : string.Empty;
        Project = requirement.Project;

        switch (resolution.Match)
        {
            case EngineMatch.Matched when resolution.Engine is { } engine:
                State = EngineState.Matched;
                Version = $"Godot {EngineRowViewModel.NameOf(engine.Tag)}";
                Note = paths.Shorten(engine.Directory, NoteLength);
                Action = "Open in Godot";

                break;

            case EngineMatch.Mismatch when resolution.Engine is { } engine:
                State = EngineState.Mismatch;
                Version = $"Godot {EngineRowViewModel.NameOf(engine.Tag)}";
                StateLabel = "mismatch";
                Note = MismatchNote(requirement, engine, resolution.IsDefault, asked);
                Action = "Open anyway";

                break;

            case EngineMatch.Missing:
                State = EngineState.Missing;
                Version = asked is null ? "No engine installed" : $"Godot {asked}";
                StateLabel = "not installed";
                Note = asked is null
                    ? "No Godot engine is installed on this machine."
                    : $"No install matches {asked}.";
                Action = "Install";

                break;

            default:
                State = EngineState.Quiet;
                Version = "No Godot project";
                Note = "Nothing in this workspace holds a project.godot.";
                Runtime = string.Empty;

                break;
        }
    }

    /// <summary>Before the first read has come back.</summary>
    public static EngineViewModel Reading { get; } = new()
    {
        Version = "Godot",
        Note = "Reading engine installs",
    };

    public static EngineViewModel NoWorkspace { get; } = new()
    {
        Version = "No workspace",
        Note = "Add a workspace to see the engine it needs.",
    };

    public EngineResolution? Resolution { get; }

    /// <summary>The project the strip is about, when there is one.</summary>
    public GodotProject? Project { get; }

    public string Version { get; private init; } = string.Empty;

    /// <summary>The runtime badge, or empty when the project is not a .NET project.</summary>
    public string Runtime { get; private init; } = string.Empty;

    public EngineState State { get; private init; } = EngineState.Quiet;

    /// <summary>What the state means, in words. Empty when there is nothing to add.</summary>
    public string StateLabel { get; private init; } = string.Empty;

    /// <summary>Where the engine is, or why there is not one.</summary>
    public string Note { get; private init; } = string.Empty;

    public string Action { get; private init; } = string.Empty;

    public bool HasRuntime => Runtime.Length > 0;

    public bool HasState => StateLabel.Length > 0;

    /// <summary>The strip has a button only when there is something for it to do.</summary>
    public bool HasAction => Action.Length > 0 && _launcher is not null;

    public bool CanOpenProject => Project is not null && _launcher is not null;

    public bool IsMatched => State == EngineState.Matched;

    public bool IsMismatch => State == EngineState.Mismatch;

    public bool IsMissing => State == EngineState.Missing;

    public string BadgeTier => State switch
    {
        EngineState.Mismatch => "Modified",
        EngineState.Missing => "Error",
        _ => "Ok",
    };

    /// <summary>
    /// Opens the project, or goes to the engines page when there is nothing to open it
    /// with. One button, and what it does follows what the strip says.
    /// </summary>
    [RelayCommand]
    private Task Run()
    {
        if (_launcher is null)
        {
            return Task.CompletedTask;
        }

        if (Resolution is { Engine: { } engine } && Project is { } project)
        {
            return _launcher.OpenInGodot(engine, project);
        }

        ShowEngines();

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenProjectFolder()
    {
        if (Project is { } project)
        {
            _launcher?.OpenFolder(project.Directory);
        }
    }

    // Not a command. The rail is how a person goes to the engines page, and the only
    // reason this exists is that Install has nowhere else to send them.
    private void ShowEngines() => _launcher?.ShowEngines();

    private static string MismatchNote(
        EngineRequirement requirement,
        InstalledEngine engine,
        bool isDefault,
        string? asked)
    {
        // The runtime is reported ahead of the version, since a .NET project in a plain
        // engine cannot build at all and a version one patch out usually still opens.
        if (engine.IsMono != requirement.NeedsDotnet)
        {
            return requirement.NeedsDotnet
                ? "This project needs C# and this engine has none."
                : "This engine carries C# support the project does not use.";
        }

        if (isDefault)
        {
            return asked is null
                ? "Using the default install."
                : $"Nothing matches {asked}. Using the default install.";
        }

        return asked is null ? "Not the engine this project names." : $"The project asks for {asked}.";
    }
}
