namespace Workbench.Core.Settings;

/// <summary>
/// Reads and writes a workspace's settings files. Loaded scopes are cached until a
/// write or a call to <see cref="Reload"/>.
/// </summary>
/// <remarks>
/// Synchronous because config files are small and callers need settings before they
/// can start. Nothing watches the filesystem, so a write in one process is not seen
/// by another until that process reloads.
/// </remarks>
public sealed class WorkspaceSettingsService : ISettingsService
{
    private readonly WorkspacePaths _paths;
    private readonly Dictionary<SettingsScope, ISettings> _cache = [];
    private readonly Lock _gate = new();

    public WorkspaceSettingsService(WorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public WorkspacePaths Paths => _paths;

    public ISettings Global => ForScope(SettingsScope.Global);

    public ISettings ForTool(string toolId) => ForScope(SettingsScope.ForTool(toolId));

    public void Set<T>(SettingsScope scope, SettingsLayer layer, string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var path = _paths.FileFor(scope, layer);

            // Load again so a change made outside this process is not reverted.
            var document = SettingsDocument.Load(path);
            document.SetValue(key, value);
            document.Save(path);

            if (layer == SettingsLayer.User)
            {
                EnsureUserLayerIgnored();
            }

            _cache.Remove(scope);
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }

    private ISettings ForScope(SettingsScope scope)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(scope, out var cached))
            {
                return cached;
            }

            // Highest precedence first.
            var settings = new LayeredSettings(
            [
                SettingsDocument.Load(_paths.FileFor(scope, SettingsLayer.User)),
                SettingsDocument.Load(_paths.FileFor(scope, SettingsLayer.TeamShared)),
            ]);

            _cache[scope] = settings;
            return settings;
        }
    }

    /// <summary>
    /// The user layer sits inside <c>.workbench</c>, so the folder carries its own
    /// ignore rule instead of relying on the repository's.
    /// </summary>
    private void EnsureUserLayerIgnored()
    {
        var path = Path.Combine(_paths.WorkbenchDirectory, ".gitignore");

        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(_paths.WorkbenchDirectory);
        File.WriteAllText(
            path,
            $"# Personal Workbench settings. Not shared.{Environment.NewLine}user/{Environment.NewLine}");
    }
}
