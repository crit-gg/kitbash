using Avalonia.Media;

namespace Workbench.ViewModels;

/// <summary>Whether the installed Godot matches what the project asks for.</summary>
public enum EngineStatus
{
    Matched,
    Mismatch,
    Missing,
}

/// <summary>The engine strip. Every color depends on the status.</summary>
public sealed class EngineViewModel
{
    private EngineViewModel(EngineStatus status)
    {
        Status = status;
    }

    public EngineStatus Status { get; }

    public required string Version { get; init; }

    public required string State { get; init; }

    public required string Note { get; init; }

    public required string Action { get; init; }

    public required IBrush PillBackground { get; init; }

    public required IBrush PillBorder { get; init; }

    public required IBrush Dot { get; init; }

    public required IBrush StateForeground { get; init; }

    public required IBrush ButtonBorder { get; init; }

    public required IBrush ButtonForeground { get; init; }

    public static EngineViewModel For(EngineStatus status) => status switch
    {
        EngineStatus.Mismatch => new EngineViewModel(status)
        {
            Version = "Godot 4.3 stable",
            State = "VERSION MISMATCH",
            Note = "project.godot requests 4.4, and 4.4.1 is installed but not selected",
            Action = "Switch to 4.4.1",
            PillBackground = Brush("#1f1a10"),
            PillBorder = Brush("#3b2f16"),
            Dot = Brush("#e0a943"),
            StateForeground = Brush("#e0a943"),
            ButtonBorder = Brush("#e0a943"),
            ButtonForeground = Brush("#e0a943"),
        },

        EngineStatus.Missing => new EngineViewModel(status)
        {
            Version = "Godot 4.4 is not installed",
            State = "NOT FOUND",
            Note = "project.godot requests 4.4, with no matching install on this machine",
            Action = "Install 4.4.1",
            PillBackground = Brush("#1e1416"),
            PillBorder = Brush("#3a1e21"),
            Dot = Brush("#ea5257"),
            StateForeground = Brush("#ea8a8a"),
            ButtonBorder = Brush("#c95f52"),
            ButtonForeground = Brush("#ea8a8a"),
        },

        _ => new EngineViewModel(EngineStatus.Matched)
        {
            Version = "Godot 4.4.1 stable",
            State = "MATCHES PROJECT",
            Note = "project.godot requests 4.4, installed at C:/godot/4.4.1",
            Action = "Open in Godot",
            PillBackground = Brush("#112019"),
            PillBorder = Brush("#23452f"),
            Dot = Brush("#52cfa5"),
            StateForeground = Brush("#7cd6b2"),
            ButtonBorder = Brush("#58a6f0"),
            ButtonForeground = Brush("#92c5f7"),
        },
    };

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);
}
