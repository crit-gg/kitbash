using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Tools;

/// <summary>
/// Scans the tools directory. One folder per tool, one folder per version inside it, and
/// each version holds the manifest it was installed from.
/// </summary>
public sealed class InstalledTools : IInstalledTools
{
    /// <summary>Where a tool's state file records the version that opens.</summary>
    private const string ActiveVersionKey = "install.version";

    /// <summary>Where a tool's state file records the folder it was pointed at.</summary>
    private const string LinkedDirectoryKey = "install.directory";

    private readonly ApplicationPaths _paths;
    private readonly IFileSystem _files;
    private readonly IToolManifestReader _manifests;
    private readonly IToolRuntime _runtime;
    private readonly IApplicationState _state;
    private readonly ToolLog _log;

    public InstalledTools(
        ApplicationPaths paths,
        IFileSystem files,
        IToolManifestReader manifests,
        IToolRuntime runtime,
        IApplicationState state,
        ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(log);

        _paths = paths;
        _files = files;
        _manifests = manifests;
        _runtime = runtime;
        _state = state;
        _log = log;
    }

    public IReadOnlyList<InstalledTool> Read()
    {
        List<InstalledTool> found = [];

        foreach (var folder in _files.EnumerateDirectories(_paths.Tools))
        {
            if (ToolId.TryParse(Path.GetFileName(folder), out var id) && Active(id, folder) is { } tool)
            {
                found.Add(tool);
            }
        }

        return [.. found.OrderBy(tool => tool.Name, StringComparer.CurrentCulture)];
    }

    public void SetActiveVersion(ToolId id, ToolVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        _state.Set(SettingsScope.ForTool(id.Value), ActiveVersionKey, version.ToString());
    }

    public void Link(ToolId id, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _state.Set(SettingsScope.ForTool(id.Value), LinkedDirectoryKey, directory);
    }

    public void Unlink(ToolId id) =>
        _state.Apply(SettingsScope.ForTool(id.Value), [SettingsEdit.Remove(LinkedDirectoryKey)]);

    /// <summary>
    /// The folder the state file points at, or the version it names, or the newest usable
    /// version when it names nothing. A folder that has gone leaves the tool out.
    /// </summary>
    private InstalledTool? Active(ToolId id, string folder)
    {
        if (Linked(id) is { } linked)
        {
            if (!_files.DirectoryExists(linked))
            {
                _log.Say($"{id} is pointed at '{linked}', which is not there");
                return null;
            }

            var tool = Describe(id, linked);

            if (tool is null)
            {
                _log.Say($"{id} is pointed at '{linked}', which holds no tool this machine can run");
            }

            return tool is null ? null : tool with { IsLinked = true };
        }

        return Versioned(id, folder);
    }

    /// <summary>
    /// Where a linked tool runs from, or null for one Kitbash installed itself. A relative
    /// path is refused rather than resolved, since it would mean a different folder to
    /// every process that read it.
    /// </summary>
    private string? Linked(ToolId id)
    {
        var directory = _state.ForTool(id.Value).Get(LinkedDirectoryKey, string.Empty);

        return !string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory)
            ? directory
            : null;
    }

    private InstalledTool? Versioned(ToolId id, string folder)
    {
        List<InstalledTool> versions = [.. _files
            .EnumerateDirectories(folder)
            .Select(version => Describe(id, version))
            .OfType<InstalledTool>()];

        if (versions.Count == 0)
        {
            return null;
        }

        var wanted = _state.ForTool(id.Value).Get(ActiveVersionKey, string.Empty);

        if (ToolVersion.TryParse(wanted, out var pinned)
            && versions.Find(version => version.Version == pinned) is { } chosen)
        {
            return chosen;
        }

        return versions.MaxBy(version => version.Version);
    }

    /// <summary>
    /// One version folder, or null when it holds nothing this machine can run. The
    /// manifest is the truth about which version it is, so the folder is only where it sits.
    /// </summary>
    private InstalledTool? Describe(ToolId id, string directory)
    {
        ToolManifest manifest;

        try
        {
            manifest = _manifests.ReadFrom(directory);
        }
        catch (ToolManifestException)
        {
            return null;
        }

        // The folder is the id and the manifest is only ever the tool's own half of it,
        // so a folder somebody renamed is left alone rather than opened under a new name.
        if (!string.Equals(manifest.Id, id.Name, StringComparison.Ordinal))
        {
            return null;
        }

        return _runtime.PayloadFor(manifest) is { } payload
            ? new InstalledTool(id, manifest.Version, directory, manifest, payload)
            : null;
    }
}
