using System.Text.Json;
using System.Text.Json.Serialization;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// Reads the IDEs JetBrains Toolbox has installed. One reader for both operating systems,
/// so the supported product codes are written once.
/// </summary>
internal sealed class JetBrainsToolbox
{
    /// <summary>
    /// The product codes with a brand mark here. An IDE Toolbox lists and this does not
    /// know is skipped rather than drawn without one.
    /// </summary>
    private static readonly string[] Supported =
    [
        "CL", "DB", "DL", "DS", "GO", "JB", "PC",
        "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS",
    ];

    /// <summary>
    /// Rider is the .NET one, so it opens a solution where it finds one. A solution means
    /// nothing to WebStorm or DataGrip, which open the folder and nothing else.
    /// </summary>
    private const string Solutions = "RD";

    private static readonly string[] SolutionFiles = [".sln", ".slnx"];

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IFileSystem _fileSystem;

    public JetBrainsToolbox(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// What is installed under the given Toolbox directory. A missing, unreadable or
    /// unparseable state file gives nothing.
    /// </summary>
    public IReadOnlyList<WorkspaceOpener> Read(string toolboxDirectory)
    {
        if (string.IsNullOrWhiteSpace(toolboxDirectory))
        {
            return [];
        }

        var file = Path.Combine(toolboxDirectory, "state.json");

        if (!_fileSystem.FileExists(file))
        {
            return [];
        }

        State? state;

        try
        {
            state = JsonSerializer.Deserialize<State>(_fileSystem.ReadAllText(file), Reading);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }

        if (state?.Tools is null)
        {
            return [];
        }

        List<WorkspaceOpener> openers = [];

        foreach (var tool in state.Tools)
        {
            if (Opener(tool) is { } opener)
            {
                openers.Add(opener);
            }
        }

        return openers;
    }

    private WorkspaceOpener? Opener(Tool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.ProductCode)
            || string.IsNullOrWhiteSpace(tool.LaunchCommand)
            || !Supported.Contains(tool.ProductCode, StringComparer.Ordinal))
        {
            return null;
        }

        // Toolbox writes this absolute on Linux and relative to the install on Windows.
        var program = Path.IsPathRooted(tool.LaunchCommand)
            ? tool.LaunchCommand
            : Path.Combine(tool.InstallLocation ?? string.Empty, tool.LaunchCommand);

        // Toolbox leaves an entry behind for an IDE that has been removed.
        if (!_fileSystem.IsExecutableFile(program))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(tool.DisplayVersion)
            ? tool.DisplayName ?? tool.ProductCode
            : $"{tool.DisplayName ?? tool.ProductCode} {tool.DisplayVersion}";

        var opensSolutions = string.Equals(tool.ProductCode, Solutions, StringComparison.Ordinal);

        return new WorkspaceOpener(
            $"jetbrains.{tool.ProductCode}",
            name,
            $"jetbrains/{tool.ProductCode}",
            program,
            WorkspaceOpenerKind.Editor)
        {
            OpensFiles = opensSolutions ? SolutionFiles : [],
            SearchDepth = opensSolutions ? 4 : 1,
        };
    }

    private sealed class State
    {
        [JsonPropertyName("tools")]
        public List<Tool>? Tools { get; set; }
    }

    private sealed class Tool
    {
        [JsonPropertyName("productCode")]
        public string? ProductCode { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("displayVersion")]
        public string? DisplayVersion { get; set; }

        [JsonPropertyName("installLocation")]
        public string? InstallLocation { get; set; }

        [JsonPropertyName("launchCommand")]
        public string? LaunchCommand { get; set; }
    }
}
