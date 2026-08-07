using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;

namespace Kitbash.Tools;

/// <summary>
/// Reads <c>tools.repositories</c> out of the global config and the open workspace's, and
/// writes the global half. A workspace's own list is only ever read here.
/// </summary>
public sealed class ToolRepositoryList : IToolRepositoryList
{
    /// <summary>The key both files hold, as an array of tables.</summary>
    private const string Key = "tools.repositories";

    private const string TypeKey = "type";
    private const string UrlKey = "url";

    /// <summary>Where an entry is listed, in words, for a refusal to name.</summary>
    private const string GlobalOrigin = "the global config";

    private readonly IApplicationSettings _application;
    private readonly IWorkspaceRegistry _workspaces;
    private readonly IWorkspaceSettingsFactory _workspaceSettings;
    private readonly ToolLog _log;

    public ToolRepositoryList(
        IApplicationSettings application,
        IWorkspaceRegistry workspaces,
        IWorkspaceSettingsFactory workspaceSettings,
        ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(workspaceSettings);
        ArgumentNullException.ThrowIfNull(log);

        _application = application;
        _workspaces = workspaces;
        _workspaceSettings = workspaceSettings;
        _log = log;
    }

    public IReadOnlyList<ToolRepositorySource> Read()
    {
        List<ToolRepositorySource> sources = [.. ReadGlobal()];

        if (_workspaces.Current is { Exists: true } workspace)
        {
            sources.AddRange(Workspace(workspace));
        }

        return sources;
    }

    public IReadOnlyList<ToolRepositorySource> ReadWorkspaces()
    {
        List<ToolRepositorySource> sources = [];

        foreach (var workspace in _workspaces.All.Where(workspace => workspace.Exists))
        {
            sources.AddRange(Workspace(workspace));
        }

        return sources;
    }

    public IReadOnlyList<ToolRepositorySource> ReadGlobal()
    {
        // The file has not been read since the last write from here, so it is read again.
        _application.Reload();

        return In(_application.Global, GlobalOrigin);
    }

    public void WriteGlobal(IReadOnlyList<ToolRepositorySource> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var rows = repositories
            .Select(repository => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [TypeKey] = repository.Type,
                [UrlKey] = repository.Url.ToString(),
            })
            .ToArray();

        _application.Apply(SettingsScope.Global, [SettingsEdit.Set(Key, rows)]);
    }

    private IReadOnlyList<ToolRepositorySource> Workspace(Workspace workspace)
    {
        try
        {
            var settings = _workspaceSettings.For(new WorkspacePaths(workspace.Root)).Global;

            return In(settings, $"the {workspace.Name} workspace", workspace.Name);
        }
        catch (SettingsFileUnreadableException exception)
        {
            _log.Say($"{workspace.Name} has a config that will not parse", exception);

            return [];
        }
    }

    /// <summary>
    /// An array of tables reads back as an array of tables, so the rows are dictionaries
    /// rather than a type the converter knows. A row missing either key is skipped.
    /// </summary>
    private IReadOnlyList<ToolRepositorySource> In(ISettings settings, string origin, string? workspace = null)
    {
        if (!settings.TryGet<object[]>(Key, out var rows))
        {
            return [];
        }

        List<ToolRepositorySource> sources = [];

        foreach (var row in rows.OfType<IReadOnlyDictionary<string, object?>>())
        {
            if (Read(row, TypeKey) is not { } type || Read(row, UrlKey) is not { } url)
            {
                _log.Say($"a repository in {origin} is missing its type or its url");
                continue;
            }

            try
            {
                sources.Add(
                    new ToolRepositorySource(type.ToLowerInvariant(), WebAddress.Parse(url), origin, workspace));
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                _log.Say($"'{url}' in {origin} is not an address", exception);
            }
        }

        return sources;
    }

    private static string? Read(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
