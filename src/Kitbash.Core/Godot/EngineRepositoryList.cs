using Kitbash.Core.Platform;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Godot;

internal sealed class EngineRepositoryList : IEngineRepositoryList
{
    /// <summary>The key every file holds, as an array of tables.</summary>
    public const string Key = "godot.repositories";

    private const string NameKey = "name";
    private const string TypeKey = "type";
    private const string UrlKey = "url";

    private readonly IApplicationSettings _application;
    private readonly ISettingsDocumentStore _documents;

    public EngineRepositoryList(IApplicationSettings application, ISettingsDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(documents);

        _application = application;
        _documents = documents;
    }

    public IReadOnlyList<EngineRepositorySource> ReadGlobal()
    {
        // Written from here since the last read, so it is read again.
        _application.Reload();

        return _application.Global.TryGet<object[]>(Key, out var rows)
            ? In(rows, "the global config")
            : [];
    }

    public IReadOnlyList<EngineRepositorySource> ReadFor(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var paths = new WorkspacePaths(workspaceRoot);
        var found = new List<EngineRepositorySource>();

        // Highest precedence first, so the first entry of a name is the one kept.
        foreach (var layer in new[] { SettingsLayer.User, SettingsLayer.TeamShared })
        {
            var file = _documents.Open(paths.FileFor(SettingsScope.Global, layer));

            if (file.ParseError is null
                && file.Document.TryGetValue(Key, out var raw)
                && raw is object[] rows)
            {
                found.AddRange(In(rows, "the workspace config"));
            }
        }

        found.AddRange(ReadGlobal());

        return
        [
            .. found
                .GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()),
        ];
    }

    public void WriteGlobal(IReadOnlyList<EngineRepositorySource> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var rows = repositories
            .Select(repository => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [NameKey] = repository.Name,
                [TypeKey] = repository.Address.Kind,
                [UrlKey] = repository.Url.ToString(),
            })
            .ToArray();

        _application.Apply(SettingsScope.Global, [SettingsEdit.Set(Key, rows)]);
    }

    /// <summary>
    /// A row missing a key, naming a kind this app has no reader for, or pointing at
    /// something that is not a repository is skipped. Nothing here can install from it.
    /// </summary>
    private static List<EngineRepositorySource> In(object[] rows, string origin)
    {
        var sources = new List<EngineRepositorySource>();

        foreach (var row in rows.OfType<IReadOnlyDictionary<string, object?>>())
        {
            if (Text(row, NameKey) is not { } name
                || Text(row, TypeKey) is not { } type
                || Text(row, UrlKey) is not { } url
                || !string.Equals(type, EngineRepositoryAddress.GitHub, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            WebAddress address;

            try
            {
                address = WebAddress.Parse(url);
            }
            catch (Exception error) when (error is ArgumentException or FormatException)
            {
                continue;
            }

            if (EngineRepositoryAddress.ForGitHub(address) is { } repository)
            {
                sources.Add(new EngineRepositorySource(name, repository, address, origin));
            }
        }

        return sources;
    }

    private static string? Text(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
