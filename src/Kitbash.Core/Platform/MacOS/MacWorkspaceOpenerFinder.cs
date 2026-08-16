using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;

namespace Kitbash.Core.Platform.MacOS;

/// <summary>
/// Finds application bundles rather than programs on PATH, since a bundle launched from
/// Finder is what a person installed and PATH here holds almost nothing.
/// </summary>
internal sealed class MacWorkspaceOpenerFinder : IWorkspaceOpenerFinder
{
    /// <summary>LaunchServices runs the bundle, since a bundle is a directory and cannot be run.</summary>
    private const string Opener = "/usr/bin/open";

    /// <summary>Everything in the VS Code family opens one of these.</summary>
    private static readonly string[] CodeWorkspaces = [".code-workspace"];

    private static readonly Editor[] Editors =
    [
        new("Visual Studio Code", "vscode", "vscode", "Visual Studio Code"),
        new("Visual Studio Code - Insiders", "code-insiders", "vscode_insiders", "Visual Studio Code Insiders"),
        new("VSCodium", "codium", "codium", "VSCodium"),
        new("Cursor", "cursor", "cursor", "Cursor"),
    ];

    /// <summary>
    /// Every terminal found is offered rather than the first one present, since a person
    /// notices being handed the wrong one.
    /// </summary>
    private static readonly Terminal[] Terminals =
    [
        new("Terminal", "apple-terminal", "apple_terminal", "Terminal"),
        new("iTerm", "iterm", "iterm", "iTerm"),
        new("Ghostty", "ghostty", "ghostty", "Ghostty"),
        new("kitty", "kitty", "kitty", "kitty"),
        new("WezTerm", "wezterm", "wezterm", "WezTerm"),
        new("Alacritty", "alacritty", "alacritty", "Alacritty"),
        new("Warp", "warp", "warp", "Warp"),
    ];

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly JetBrainsToolbox _toolbox;

    public MacWorkspaceOpenerFinder(
        IEnvironment environment,
        IFileSystem fileSystem,
        JetBrainsToolbox toolbox)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(toolbox);

        _environment = environment;
        _fileSystem = fileSystem;
        _toolbox = toolbox;
    }

    public Task<IReadOnlyList<WorkspaceOpener>> FindAsync(CancellationToken cancellation = default)
    {
        List<WorkspaceOpener> openers = [];

        foreach (var editor in Editors)
        {
            if (Look(editor.Bundle) is { } bundle)
            {
                openers.Add(new WorkspaceOpener(
                    editor.Id, editor.Name, editor.Icon, Opener, WorkspaceOpenerKind.Editor)
                {
                    FixedArguments = ["-a", bundle],
                    OpensFiles = CodeWorkspaces,
                    SearchDepth = 2,
                });
            }
        }

        openers.AddRange(_toolbox.Read(ToolboxDirectory()));

        foreach (var terminal in Terminals)
        {
            if (Look(terminal.Bundle) is { } bundle)
            {
                openers.Add(new WorkspaceOpener(
                    terminal.Id, terminal.Name, $"terminal/{terminal.Icon}", Opener, WorkspaceOpenerKind.Terminal)
                {
                    // open takes no working directory, so the folder is the only way to say
                    // where a terminal opens. It reaches LaunchServices rather than a shell,
                    // so it is not read as a command to run.
                    FixedArguments = ["-a", bundle],
                    TakesPathArgument = true,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<WorkspaceOpener>>(openers);
    }

    /// <summary>
    /// The bundle for an application name, or null. A bundle counts as installed only when
    /// it holds a program, so an empty folder left by an uninstall is not offered.
    /// </summary>
    private string? Look(string application)
    {
        foreach (var root in Roots())
        {
            var bundle = Path.Combine(root, application + ".app");
            var executables = Path.Combine(bundle, "Contents", "MacOS");

            if (_fileSystem.DirectoryExists(executables)
                && _fileSystem.EnumerateFiles(executables, recursive: false).Any(_fileSystem.IsExecutableFile))
            {
                return bundle;
            }
        }

        return null;
    }

    /// <summary>
    /// Where a bundle can be. Utilities is where Terminal has lived since Catalina, and the
    /// home folder is where a per user install and JetBrains Toolbox put theirs.
    /// </summary>
    private IEnumerable<string> Roots()
    {
        yield return "/Applications";
        yield return "/Applications/Utilities";
        yield return "/System/Applications";
        yield return "/System/Applications/Utilities";

        var home = _environment.GetHomeDirectory();

        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, "Applications");
        }
    }

    private string ToolboxDirectory() =>
        Path.Combine(
            _environment.GetHomeDirectory(),
            "Library",
            "Application Support",
            "JetBrains",
            "Toolbox");

    private sealed record Editor(string Bundle, string Id, string Icon, string Name);

    private sealed record Terminal(string Bundle, string Id, string Icon, string Name);
}
