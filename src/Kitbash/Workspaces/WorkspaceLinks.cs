using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Ui.Controls;

namespace Kitbash.Workspaces;

/// <summary>
/// Reads and writes <c>workspace.links</c> in a workspace's own config. Each layer is
/// read on its own and the two are added together, so a personal list joins the team's
/// rather than hiding it the way a merged key would.
/// </summary>
public sealed class WorkspaceLinks : IWorkspaceLinks
{
    /// <summary>The key both layers hold, as an array of tables.</summary>
    private const string Key = "workspace.links";

    private const string LabelKey = "label";
    private const string UrlKey = "url";
    private const string IconKey = "icon";

    /// <summary>What a row with no icon takes, and what an unknown name falls back to.</summary>
    private const IconGlyph Fallback = IconGlyph.Link;

    /// <summary>Lowest first, so the team's links keep the head of the list.</summary>
    private static readonly SettingsLayer[] Layers = [SettingsLayer.TeamShared, SettingsLayer.User];

    private readonly IWorkspaceRegistry _workspaces;
    private readonly IWorkspaceSettingsFactory _settings;
    private readonly WorkspaceLinkIcons _icons;
    private readonly WorkspaceLog _log;

    public WorkspaceLinks(
        IWorkspaceRegistry workspaces,
        IWorkspaceSettingsFactory settings,
        WorkspaceLinkIcons icons,
        WorkspaceLog log)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(log);

        _workspaces = workspaces;
        _settings = settings;
        _icons = icons;
        _log = log;
    }

    public IReadOnlyList<WorkspaceLink> Read()
    {
        if (_workspaces.Current is not { Exists: true } workspace)
        {
            return [];
        }

        List<WorkspaceLink> links = [];

        foreach (var layer in Layers)
        {
            try
            {
                links.AddRange(Parse(ReadEntries(workspace.Root, layer), workspace.Name));
            }
            catch (SettingsFileUnreadableException exception)
            {
                _log.Say($"{workspace.Name} has a config that will not parse", exception);
            }
        }

        return links;
    }

    public IReadOnlyList<WorkspaceLinkEntry> ReadEntries(string root, SettingsLayer layer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var settings = _settings.For(new WorkspacePaths(root)).In(SettingsScope.Global, layer);

        if (!settings.TryGet<object[]>(Key, out var rows))
        {
            return [];
        }

        // An array of tables reads back as an array of tables, so the rows are
        // dictionaries rather than a type the converter knows.
        return
        [
            .. rows.OfType<IReadOnlyDictionary<string, object?>>()
                .Select(row => new WorkspaceLinkEntry(
                    Text(row, LabelKey),
                    Text(row, UrlKey),
                    Text(row, IconKey))),
        ];
    }

    public void Write(string root, SettingsLayer layer, IReadOnlyList<WorkspaceLinkEntry> links)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(links);

        var rows = links
            .Select(link =>
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [LabelKey] = link.Label,
                    [UrlKey] = link.Url,
                };

                // A row the file never named an icon for keeps no icon, so nothing writes
                // a default in that a person would then have to take out again.
                if (link.Icon.Length > 0)
                {
                    row[IconKey] = link.Icon;
                }

                return row;
            })
            .ToArray();

        _settings.For(new WorkspacePaths(root))
            .Apply(SettingsScope.Global, layer, [SettingsEdit.Set(Key, rows)]);
    }

    /// <summary>The rows Kitbash can draw as links. A row missing either word is skipped.</summary>
    private IEnumerable<WorkspaceLink> Parse(IReadOnlyList<WorkspaceLinkEntry> entries, string workspace)
    {
        foreach (var entry in entries)
        {
            if (entry.Label.Length == 0 || entry.Url.Length == 0)
            {
                _log.Say($"a link in {workspace} is missing its label or its url");
                continue;
            }

            WorkspaceLink? link;

            try
            {
                link = new WorkspaceLink(entry.Label, WebAddress.Parse(entry.Url), Icon(entry.Icon, workspace));
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                _log.Say($"'{entry.Url}' in {workspace} is not an address", exception);
                link = null;
            }

            if (link is not null)
            {
                yield return link;
            }
        }
    }

    /// <summary>
    /// The set of glyphs is closed, so a name outside it draws the link mark rather than
    /// taking the row away. Box Icons names them with hyphens and the enum does not.
    /// </summary>
    private IconGlyph Icon(string name, string workspace)
    {
        if (name.Length == 0)
        {
            return Fallback;
        }

        if (_icons.TryRead(name, out var glyph))
        {
            return glyph;
        }

        _log.Say($"'{name}' in {workspace} is not an icon Kitbash draws");

        return Fallback;
    }

    private static string Text(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is string text ? text.Trim() : string.Empty;
}
