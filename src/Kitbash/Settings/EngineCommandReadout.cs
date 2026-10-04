using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings.Schema;
using Kitbash.ViewModels;

namespace Kitbash.Settings;

/// <summary>The row under the PATH setting, saying where the godot command stands.</summary>
public sealed class EngineCommandReadout
{
    private const int PathWidth = 96;

    private readonly IPathShortener _shortener;

    public EngineCommandReadout(IEngineCommand command, IPathShortener shortener)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(shortener);

        _shortener = shortener;

        // Syncing as it reads means a page saved with the setting on describes the command
        // that save just made. A readout is read off the UI thread, so waiting is allowed.
        Row = new SettingsReadoutRow
        {
            Name = "godot command",
            Style = SettingsReadoutStyle.List,
            Layout = SettingsRowLayout.Below,
            HiddenWhenEmpty = true,
            Read = () => Describe(command.SyncAsync(CancellationToken.None).GetAwaiter().GetResult()),
        };
    }

    public SettingsReadoutRow Row { get; }

    public IReadOnlyList<SettingsListEntry> Describe(EngineCommandState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var command = state.Command;
        var folder = command is null ? null : Path.GetDirectoryName(command);

        return state.Kind switch
        {
            EngineCommandKind.Off => [],
            EngineCommandKind.Unavailable when command is null => [new("There is no home folder to put it in.")],
            EngineCommandKind.Unavailable => [new("This copy of Kitbash cannot put Godot on PATH.")],
            EngineCommandKind.NoDefault => [new("No default engine is set. Name one on the engines page.")],
            EngineCommandKind.Conflict =>
                [About(command!, $"{Short(command!)} was not made by Kitbash. Remove it to let Kitbash write one.")],
            EngineCommandKind.Failed => [About(command, $"Could not write {Short(command)}. {state.Failure}".Trim())],
            _ => Placed(state, command!, folder!),
        };
    }

    private List<SettingsListEntry> Placed(EngineCommandState state, string command, string folder)
    {
        var lines = new List<SettingsListEntry>();

        if (state.Engine is { } engine)
        {
            var name = EngineRowViewModel.DisplayName(engine.Id) + (engine.IsMono ? " .NET" : string.Empty);
            lines.Add(new($"{Short(command)} opens Godot {name}", IsCurrent: true, Full: command));
        }

        switch (state.Reach?.Path)
        {
            case PathReach.Added:
                lines.Add(new("Open a new terminal to use it."));
                break;
            case PathReach.NotOnPath:
                lines.Add(About(folder, $"{Short(folder)} is not on PATH. Add it to run godot from a terminal."));
                break;
            case PathReach.Declined:
                lines.Add(About(folder, $"{Short(folder)} is not on PATH. Turn this off and on to be asked again."));
                break;
        }

        if (state.Reach?.Shadow is { } shadow)
        {
            lines.Add(About(shadow, $"{Short(shadow)} comes first on PATH, so godot runs that one."));
        }

        return lines;
    }

    private string Short(string? path) => path is null ? "godot" : _shortener.Shorten(path, PathWidth);

    // The path alone is what copying gives, since that is the part worth pasting somewhere.
    private static SettingsListEntry About(string? path, string text) => new(text, Full: path);
}
