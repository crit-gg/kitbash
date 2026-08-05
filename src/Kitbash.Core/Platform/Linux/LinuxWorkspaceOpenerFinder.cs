using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Assumes no distribution and no desktop. Every candidate is looked up rather than
/// assumed present, and nothing here names a package manager.
/// </summary>
internal sealed class LinuxWorkspaceOpenerFinder : IWorkspaceOpenerFinder
{
    /// <summary>Everything in the VS Code family opens one of these.</summary>
    private static readonly string[] CodeWorkspaces = [".code-workspace"];

    private static readonly Editor[] Editors =
    [
        new("code", "vscode", "Visual Studio Code"),
        new("code-insiders", "vscode_insiders", "Visual Studio Code Insiders"),
        new("codium", "codium", "VSCodium"),
        new("cursor", "cursor", "Cursor"),
    ];

    /// <summary>
    /// A terminal is offered by name rather than the first one present winning, since a
    /// person notices which terminal opened where they cannot tell one opener from another.
    /// </summary>
    private static readonly Terminal[] Terminals =
    [
        new("gnome-terminal", "gnome_terminal", "GNOME Terminal", []),
        new("konsole", "konsole", "Konsole", []),
        new("xfce4-terminal", "xfce4_terminal", "Xfce Terminal", []),
        new("lxterminal", "lxterminal", "LXTerminal", []),
        new("deepin-terminal", "deepin_terminal", "Deepin Terminal", []),
        new("mate-terminal", "mate_terminal", "MATE Terminal", []),
        new("foot", "foot", "Foot", []),
        new("ghostty", "ghostty", "Ghostty", []),
        new("kitty", "kitty", "kitty", []),
        new("wezterm", "wezterm", "WezTerm", ["start", "--cwd", "."]),
        new("ptyxis", "ptyxis", "Ptyxis", ["--new-window", "--working-directory=."]),
    ];

    private readonly IExecutableFinder _executables;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly JetBrainsToolbox _toolbox;

    public LinuxWorkspaceOpenerFinder(
        IExecutableFinder executables,
        IEnvironment environment,
        IFileSystem fileSystem,
        JetBrainsToolbox toolbox)
    {
        ArgumentNullException.ThrowIfNull(executables);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(toolbox);

        _executables = executables;
        _environment = environment;
        _fileSystem = fileSystem;
        _toolbox = toolbox;
    }

    public Task<IReadOnlyList<WorkspaceOpener>> FindAsync(CancellationToken cancellation = default)
    {
        List<WorkspaceOpener> openers = [];

        foreach (var editor in Editors)
        {
            if (Look(editor.Command) is { } program)
            {
                openers.Add(new WorkspaceOpener(
                    editor.Command, editor.Name, editor.Icon, program, WorkspaceOpenerKind.Editor)
                {
                    OpensFiles = CodeWorkspaces,
                    SearchDepth = 2,
                });
            }
        }

        openers.AddRange(_toolbox.Read(ToolboxDirectory()));

        foreach (var terminal in Terminals)
        {
            if (Look(terminal.Command) is { } program)
            {
                openers.Add(new WorkspaceOpener(
                    terminal.Command, terminal.Name, $"terminal/{terminal.Icon}", program, WorkspaceOpenerKind.Terminal)
                {
                    FixedArguments = terminal.Arguments,
                    TakesPathArgument = false,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<WorkspaceOpener>>(openers);
    }

    /// <summary>
    /// PATH first, then ~/.local/bin, which a per user install writes to and a login shell
    /// puts on PATH but a desktop session often does not. The fallback is here rather than
    /// in IExecutableFinder, since that would quietly move how git and dotnet resolve.
    /// </summary>
    private string? Look(string command)
    {
        if (_executables.Find(command) is { } found)
        {
            return found;
        }

        var local = Path.Combine(_environment.GetHomeDirectory(), ".local", "bin", command);

        return _fileSystem.IsExecutableFile(local) ? local : null;
    }

    /// <summary>
    /// The base directory specification says a relative XDG_DATA_HOME is ignored, which is
    /// the rule LinuxUserDirectories already follows.
    /// </summary>
    private string ToolboxDirectory()
    {
        var data = _environment.GetVariable("XDG_DATA_HOME");

        if (string.IsNullOrWhiteSpace(data) || !Path.IsPathRooted(data))
        {
            data = Path.Combine(_environment.GetHomeDirectory(), ".local", "share");
        }

        return Path.Combine(data, "JetBrains", "Toolbox");
    }

    private sealed record Editor(string Command, string Icon, string Name);

    private sealed record Terminal(string Command, string Icon, string Name, IReadOnlyList<string> Arguments);
}
