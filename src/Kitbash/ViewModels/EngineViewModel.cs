using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

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
                Version = $"Godot {EngineRowViewModel.VersionOf(engine.Tag)}";
                Channel = ChannelOf(engine.Tag.Channel);
                ChannelTier = TierOf(engine.Tag.Channel);
                Note = paths.Shorten(engine.Directory, NoteLength);
                Action = "Open Project";

                break;

            case EngineMatch.Mismatch when resolution.Engine is { } engine:
                State = EngineState.Mismatch;
                Version = $"Godot {EngineRowViewModel.VersionOf(engine.Tag)}";
                Channel = ChannelOf(engine.Tag.Channel);
                ChannelTier = TierOf(engine.Tag.Channel);
                StateLabel = "mismatch";
                Note = MismatchNote(requirement, engine, resolution.IsDefault, asked);

                // The button installs what was asked for. Opening what is here is the
                // workaround, so it lives in the menu.
                Action = requirement.Version is { } fix ? $"Install {NumberOf(fix)}" : "Install";

                break;

            case EngineMatch.Missing:
                State = EngineState.Missing;
                StateLabel = "not installed";

                // What was asked for is all there is to show, so it is shown the way an
                // engine is: the number, then what it says about itself in pills.
                if (requirement.Version is { } wanted)
                {
                    Version = $"Godot {NumberOf(wanted)}";
                    Channel = wanted.Channel is { } named ? ChannelOf(named) : string.Empty;
                    ChannelTier = wanted.Channel is { } tiered ? TierOf(tiered) : BadgeTier.Neutral;
                    Note = $"No install matches {wanted}.";
                    Action = $"Install {NumberOf(wanted)}";
                }
                else
                {
                    Version = "No engine installed";
                    Note = "No Godot engine is installed on this machine.";
                    Action = "Install";
                }

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

    /// <summary>
    /// The channel as a pill, such as <c>STABLE</c>. Empty only when nothing names one,
    /// which is a requirement like <c>4.7</c> with no engine behind it yet.
    /// </summary>
    public string Channel { get; private init; } = string.Empty;

    public BadgeTier ChannelTier { get; private init; } = BadgeTier.Neutral;

    public bool HasChannel => Channel.Length > 0;

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

    /// <summary>The tier of the state pill, which is the one that says how things are.</summary>
    public BadgeTier StateTier => State switch
    {
        EngineState.Mismatch => BadgeTier.Modified,
        EngineState.Missing => BadgeTier.Error,
        _ => BadgeTier.Ok,
    };

    /// <summary>True when there is an engine and a project, so either mode can start.</summary>
    public bool CanLaunch => Resolution?.Engine is not null && Project is not null && _launcher is not null;

    /// <summary>
    /// Opens the project in the editor, or goes to the engines page when there is nothing
    /// to open it with. One button, and what it does follows what the strip says.
    /// </summary>
    [RelayCommand]
    private Task Run()
    {
        if (_launcher is null)
        {
            return Task.CompletedTask;
        }

        // A match opens.
        if (State == EngineState.Matched
            && Resolution is { Engine: { } engine }
            && Project is { } project)
        {
            return _launcher.OpenInGodot(engine, project, GodotLaunchMode.Editor);
        }

        // Installs what was asked for in one press, since the workspace has already
        // named the version it wants.
        if (Resolution?.Requirement is { Version: { } wanted } requirement)
        {
            return _launcher.InstallEngineAsync(wanted, requirement.NeedsDotnet);
        }

        ShowEngines();

        return Task.CompletedTask;
    }

    /// <summary>
    /// True on a mismatch, which is the one state with an engine that is not the right
    /// one. Everywhere else the button already opens or there is nothing to open with.
    /// </summary>
    public bool CanOpenAnyway => IsMismatch && CanLaunch;

    /// <summary>Opens in the engine that is here, knowing it is not the one asked for.</summary>
    [RelayCommand]
    private Task OpenAnyway() => Launch(GodotLaunchMode.Editor);

    /// <summary>
    /// Runs the project rather than editing it. The same build and the same import happen
    /// first, since a project that will not build will not run and one that was never
    /// imported has no resources to run with.
    /// </summary>
    [RelayCommand]
    private Task Play() => Launch(GodotLaunchMode.Play);

    /// <summary>
    /// Throws the import cache away and makes it again. For the case where Godot itself
    /// is what is wrong rather than the project, which is what a stale cache looks like.
    /// It reports and stops, so opening the editor afterwards is a second press.
    /// </summary>
    [RelayCommand]
    private Task CleanRebuild() => Launch(GodotLaunchMode.Rebuild);

    private Task Launch(GodotLaunchMode mode) =>
        Resolution is { Engine: { } engine } && Project is { } project && _launcher is not null
            ? _launcher.OpenInGodot(engine, project, mode)
            : Task.CompletedTask;

    [RelayCommand]
    private void OpenProjectFolder()
    {
        if (Project is { } project)
        {
            _launcher?.OpenFolder(project.Directory);
        }
    }

    // Not a command. The rail is how a person goes to the engines page, and the only
    // reason this exists is that Install has nowhere else to send them when the workspace
    // names no version at all.
    private void ShowEngines() => _launcher?.ShowEngines();

    /// <summary>The numbers alone, such as <c>4.7.1</c>, from what was asked for.</summary>
    private static string NumberOf(EngineVersionPattern wanted)
    {
        var text = wanted.Major.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (wanted.Minor is { } minor)
        {
            text += $".{minor}";
        }

        return wanted.Patch is { } patch ? $"{text}.{patch}" : text;
    }

    private static string ChannelOf(EngineChannel channel) => channel switch
    {
        EngineChannel.Stable => "STABLE",
        EngineChannel.Rc => "RC",
        EngineChannel.Beta => "BETA",
        EngineChannel.Alpha => "ALPHA",
        _ => "DEV",
    };

    private static BadgeTier TierOf(EngineChannel channel) => channel switch
    {
        EngineChannel.Rc => BadgeTier.Modified,
        EngineChannel.Beta => BadgeTier.Graph,
        EngineChannel.Alpha => BadgeTier.Error,
        _ => BadgeTier.Neutral,
    };

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