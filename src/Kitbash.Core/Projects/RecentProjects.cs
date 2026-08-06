using System.Globalization;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Projects;

internal sealed class RecentProjects : IRecentProjects
{
    private const string PathsKey = "projects.recent.paths";
    private const string NamesKey = "projects.recent.names";
    private const string OpenedKey = "projects.recent.opened";

    private readonly RecentProjectsOptions _options;
    private readonly IApplicationState _state;
    private readonly IFileSystem _fileSystem;
    private readonly IPathRules _paths;
    private readonly Lock _gate = new();

    private List<RecentProject> _all = [];

    public RecentProjects(
        RecentProjectsOptions options,
        IApplicationState state,
        IFileSystem fileSystem,
        IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);

        _options = options;
        _state = state;
        _fileSystem = fileSystem;
        _paths = paths;

        Refresh();
    }

    public IReadOnlyList<RecentProject> All
    {
        get
        {
            lock (_gate)
            {
                return _all;
            }
        }
    }

    public RecentProject Remember(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var full = Path.GetFullPath(path);

        lock (_gate)
        {
            var stored = Read();
            stored.RemoveAll(entry => _paths.AreSame(entry.Path, full));
            stored.Insert(0, new Stored(full, name.Trim(), DateTimeOffset.UtcNow));

            Write(stored);
        }

        Refresh();

        return All.First(entry => _paths.AreSame(entry.Path, full));
    }

    public void Rename(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var full = Path.GetFullPath(path);

        lock (_gate)
        {
            var stored = Read();
            var index = stored.FindIndex(entry => _paths.AreSame(entry.Path, full));

            if (index < 0)
            {
                return;
            }

            stored[index] = stored[index] with { Name = name.Trim() };

            Write(stored);
        }

        Refresh();
    }

    public void Forget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);

        lock (_gate)
        {
            var stored = Read();

            if (stored.RemoveAll(entry => _paths.AreSame(entry.Path, full)) == 0)
            {
                return;
            }

            Write(stored);
        }

        Refresh();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            _state.Reload();

            _all = [.. Read().Select(Resolve)];
        }
    }

    private RecentProject Resolve(Stored entry) =>
        new(entry.Path, entry.Name, entry.Opened, Exists(entry.Path));

    // A project is a folder for most apps and a file for some, so either counts as there.
    private bool Exists(string path) =>
        _fileSystem.DirectoryExists(path) || _fileSystem.FileExists(path);

    /// <summary>
    /// The three arrays read back as entries, newest first. They are written together and
    /// can only be out of step if somebody edited the file, so a short one truncates the
    /// list rather than pairing a path with another entry's name.
    /// </summary>
    private List<Stored> Read()
    {
        var settings = _state.Global;

        if (_options.Scope.ToolId is { } toolId)
        {
            settings = _state.ForTool(toolId);
        }

        var paths = settings.Get(PathsKey, Array.Empty<string>());
        var names = settings.Get(NamesKey, Array.Empty<string>());
        var opened = settings.Get(OpenedKey, Array.Empty<string>());

        var stored = new List<Stored>(paths.Length);

        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            stored.Add(new Stored(
                path,
                index < names.Length && !string.IsNullOrWhiteSpace(names[index])
                    ? names[index]
                    : NameOf(path),
                index < opened.Length ? Stamp(opened[index]) : null));
        }

        return stored;
    }

    private void Write(List<Stored> stored)
    {
        if (stored.Count > _options.Limit)
        {
            stored.RemoveRange(_options.Limit, stored.Count - _options.Limit);
        }

        _state.Apply(_options.Scope,
        [
            SettingsEdit.Set(PathsKey, stored.Select(entry => entry.Path).ToArray()),
            SettingsEdit.Set(NamesKey, stored.Select(entry => entry.Name).ToArray()),
            SettingsEdit.Set(OpenedKey, stored.Select(Written).ToArray()),
        ]);
    }

    // Round trip and UTC, so a machine that moves zone reads back the instant it wrote.
    private static string Written(Stored entry) =>
        entry.Opened?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) ?? string.Empty;

    private static DateTimeOffset? Stamp(string? written) =>
        DateTimeOffset.TryParse(
            written,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;

    /// <summary>The last segment, for an entry stored with no name of its own.</summary>
    private static string NameOf(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);

        return string.IsNullOrEmpty(name) ? path : name;
    }

    private sealed record Stored(string Path, string Name, DateTimeOffset? Opened);
}
