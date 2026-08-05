using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;

namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// Microsoft.Win32.Registry is annotated windows with no version, so the platform test in
/// the factory satisfies CA1416 here. That is why this needs none of the per runtime
/// gating the secret store has.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsWorkspaceOpenerFinder : IWorkspaceOpenerFinder
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

    private static readonly string[] CodeWorkspaces = [".code-workspace"];
    private static readonly string[] Solutions = [".sln", ".slnx"];

    /// <summary>
    /// Each installer writes its exe path into DisplayIcon on its own uninstall key. The
    /// system wide install is looked at before the per user one.
    /// </summary>
    private static readonly Editor[] Editors =
    [
        new("vscode", "vscode", "Visual Studio Code",
            "{EA457B21-F73E-494C-ACAB-524FDE069978}_is1", "{771FD6B0-FA20-440A-A002-3B3BAC16DC50}_is1"),
        new("code-insiders", "vscode_insiders", "Visual Studio Code Insiders",
            "{1287CAD5-7C8D-410D-88B9-0D1EE4A83FF2}_is1", "{217B4C08-948D-4276-BFBB-BEE930AE5A2C}_is1"),
        new("codium", "codium", "VSCodium",
            "{88DA3577-054F-4CA1-8122-7D820494CFFB}_is1", "{2E1F05D1-C245-4562-81EE-28188DB6FD17}_is1"),
    ];

    private static readonly Terminal[] Terminals =
    [
        new("wt", "wt", "Windows Terminal", ["-d", "."]),
        new("pwsh", "pwsh", "PowerShell", []),
        new("powershell", "pwsh", "Windows PowerShell", []),
        new("cmd", "cmd", "Command Prompt", []),
    ];

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IExecutableFinder _executables;
    private readonly IProcessRunner _processes;
    private readonly IExternalTools _tools;
    private readonly JetBrainsToolbox _toolbox;

    public WindowsWorkspaceOpenerFinder(
        IEnvironment environment,
        IFileSystem fileSystem,
        IExecutableFinder executables,
        IProcessRunner processes,
        IExternalTools tools,
        JetBrainsToolbox toolbox)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(executables);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(toolbox);

        _environment = environment;
        _fileSystem = fileSystem;
        _executables = executables;
        _processes = processes;
        _tools = tools;
        _toolbox = toolbox;
    }

    public async Task<IReadOnlyList<WorkspaceOpener>> FindAsync(CancellationToken cancellation = default)
    {
        List<WorkspaceOpener> openers = [];

        foreach (var editor in Editors)
        {
            if (Installed(editor) is { } program)
            {
                openers.Add(new WorkspaceOpener(
                    editor.Id, editor.Name, editor.Icon, program, WorkspaceOpenerKind.Editor)
                {
                    OpensFiles = CodeWorkspaces,
                    SearchDepth = 2,
                });
            }
        }

        // Cursor writes no uninstall key worth reading, so its install path is the probe.
        var localAppData = _environment.GetVariable("LOCALAPPDATA");

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var cursor = Path.Combine(localAppData, "Programs", "Cursor", "Cursor.exe");

            if (_fileSystem.IsExecutableFile(cursor))
            {
                openers.Add(new WorkspaceOpener("cursor", "Cursor", "cursor", cursor, WorkspaceOpenerKind.Editor)
                {
                    OpensFiles = CodeWorkspaces,
                    SearchDepth = 2,
                });
            }

            openers.AddRange(_toolbox.Read(Path.Combine(localAppData, "JetBrains", "Toolbox")));
        }

        openers.AddRange(await VisualStudioAsync(cancellation).ConfigureAwait(false));
        openers.AddRange(Shells());

        return openers;
    }

    /// <summary>The exe path each installer writes into DisplayIcon, or null.</summary>
    private string? Installed(Editor editor)
    {
        var program = Value(RegistryHive.LocalMachine, editor.SystemKey)
            ?? Value(RegistryHive.CurrentUser, editor.UserKey);

        return program is not null && _fileSystem.IsExecutableFile(program) ? program : null;
    }

    private static string? Value(RegistryHive hive, string key)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var entry = root.OpenSubKey(Uninstall + key);

            return entry?.GetValue("DisplayIcon") as string;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// vswhere is the only supported way to find an installation. It ships with the
    /// installer, so its absence means no Visual Studio.
    /// </summary>
    private async Task<IReadOnlyList<WorkspaceOpener>> VisualStudioAsync(CancellationToken cancellation)
    {
        var programFiles = _environment.GetVariable("ProgramFiles(x86)");

        if (string.IsNullOrWhiteSpace(programFiles))
        {
            return [];
        }

        var vswhere = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (!_fileSystem.IsExecutableFile(vswhere))
        {
            return [];
        }

        ProcessOutput output;

        try
        {
            // A null encoding takes the console's, which here is the OEM code page, so the
            // -utf8 that was just asked for would be decoded as something else.
            output = await _processes.ReadAsync(
                new ProcessRequest(vswhere, ["-format", "json", "-prerelease", "-utf8"], UseShellExecute: false)
                {
                    TextEncoding = Encoding.UTF8,
                },
                cancellation).ConfigureAwait(false);
        }
        catch (ProcessStartException)
        {
            return [];
        }

        if (!output.Succeeded)
        {
            return [];
        }

        List<Instance>? instances;

        try
        {
            instances = JsonSerializer.Deserialize<List<Instance>>(output.StandardOutput, Reading);
        }
        catch (JsonException)
        {
            return [];
        }

        List<WorkspaceOpener> openers = [];

        foreach (var instance in instances ?? [])
        {
            if (string.IsNullOrWhiteSpace(instance.ProductPath)
                || string.IsNullOrWhiteSpace(instance.DisplayName)
                || !_fileSystem.IsExecutableFile(instance.ProductPath))
            {
                continue;
            }

            openers.Add(new WorkspaceOpener(
                $"visualstudio.{instance.DisplayName}",
                instance.DisplayName,
                instance.IsPrerelease ? "vs_preview" : "vs",
                instance.ProductPath,
                WorkspaceOpenerKind.Editor)
            {
                // A solution where there is one, and the folder otherwise. SourceGit offers
                // no folder here at all, which hides Visual Studio from a workspace that has
                // not been given a solution yet.
                OpensFiles = Solutions,
                SearchDepth = 4,
            });
        }

        return openers;
    }

    private IReadOnlyList<WorkspaceOpener> Shells()
    {
        List<WorkspaceOpener> openers = [];

        foreach (var terminal in Terminals)
        {
            // ExecutableFinder applies PATHEXT, so the name is looked up without .exe.
            if (_executables.Find(terminal.Command) is not { } program)
            {
                continue;
            }

            openers.Add(new WorkspaceOpener(
                terminal.Command, terminal.Name, $"terminal/{terminal.Icon}", program, WorkspaceOpenerKind.Terminal)
            {
                FixedArguments = terminal.Arguments,
                TakesPathArgument = false,
            });
        }

        // Git for Windows ships git-bash.exe beside the bin holding git, so the git already
        // being used is what says where it is.
        if (_tools.Git.Path is { } git
            && Path.GetDirectoryName(Path.GetDirectoryName(git)) is { } gitRoot)
        {
            var bash = Path.Combine(gitRoot, "git-bash.exe");

            if (_fileSystem.IsExecutableFile(bash))
            {
                openers.Add(new WorkspaceOpener(
                    "git-bash", "Git Bash", "terminal/git_bash", bash, WorkspaceOpenerKind.Terminal)
                {
                    TakesPathArgument = false,
                });
            }
        }

        return openers;
    }

    private sealed record Editor(string Id, string Icon, string Name, string SystemKey, string UserKey);

    private sealed record Terminal(string Command, string Icon, string Name, IReadOnlyList<string> Arguments);

    private sealed class Instance
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("productPath")]
        public string? ProductPath { get; set; }

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease { get; set; }
    }
}
