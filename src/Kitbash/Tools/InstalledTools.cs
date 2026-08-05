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

    private readonly ApplicationPaths _paths;
    private readonly IFileSystem _files;
    private readonly IToolManifestReader _manifests;
    private readonly IToolRuntime _runtime;
    private readonly IApplicationState _state;

    public InstalledTools(
        ApplicationPaths paths,
        IFileSystem files,
        IToolManifestReader manifests,
        IToolRuntime runtime,
        IApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(state);

        _paths = paths;
        _files = files;
        _manifests = manifests;
        _runtime = runtime;
        _state = state;
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

    /// <summary>
    /// The version the state file names, or the newest usable one when it names nothing
    /// or names a version that is no longer here.
    /// </summary>
    private InstalledTool? Active(ToolId id, string folder)
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
