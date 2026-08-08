using Kitbash.Core.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>
/// One workspace's links, edited in the settings window. It is its own editor because
/// <c>workspace.links</c> is an array of tables and no descriptor can describe one.
/// </summary>
public sealed class WorkspaceLinksEditor : SettingsListEditor<WorkspaceLinkRowViewModel>
{
    /// <summary>Both files are held at once, so moving the picker loses no staged work.</summary>
    private static readonly SettingsLayer[] Layers = [SettingsLayer.TeamShared, SettingsLayer.User];

    private readonly IWorkspaceLinks _links;
    private readonly WorkspaceLinkIcons _icons;
    private readonly string _root;

    /// <summary>The rows of the layer that is not being drawn, held rather than reread.</summary>
    private readonly Dictionary<SettingsLayer, List<WorkspaceLinkRowViewModel>> _staged =
        Layers.ToDictionary(layer => layer, _ => new List<WorkspaceLinkRowViewModel>());

    /// <summary>What each file said when it was last read. Dirty is measured against it.</summary>
    private readonly Dictionary<SettingsLayer, IReadOnlyList<WorkspaceLinkEntry>> _stored =
        Layers.ToDictionary(layer => layer, _ => (IReadOnlyList<WorkspaceLinkEntry>)[]);

    /// <summary>A layer whose file will not parse is never written over.</summary>
    private readonly HashSet<SettingsLayer> _unreadable = [];

    private SettingsLayer? _layer = SettingsLayer.TeamShared;

    public WorkspaceLinksEditor(IWorkspaceLinks links, WorkspaceLinkIcons icons, string root)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _links = links;
        _icons = icons;
        _root = root;

        Icons = [.. icons.All.Select(glyph => new WorkspaceLinkIcon(glyph, icons.Write(glyph)))];
    }

    /// <summary>Every glyph, which is what a row's dropdown offers.</summary>
    public IReadOnlyList<WorkspaceLinkIcon> Icons { get; }

    /// <summary>
    /// Which file this page is pointed at. Moving it swaps what is drawn and nothing else,
    /// so anything staged on the layer being left is still there on the way back.
    /// </summary>
    public override SettingsLayer? Layer
    {
        get => _layer;
        set
        {
            if (_layer == value)
            {
                return;
            }

            _staged[Current] = [.. Rows];
            _layer = value;

            Show(Current);
        }
    }

    public override bool IsDirty => Layers.Any(Differs);

    public override async Task LoadAsync(CancellationToken token = default)
    {
        var read = await Task.Run(Read, token).ConfigureAwait(true);

        _unreadable.Clear();

        foreach (var (layer, entries) in read)
        {
            if (entries is null)
            {
                _unreadable.Add(layer);
                _stored[layer] = [];
                continue;
            }

            _stored[layer] = entries;
        }

        Fill();
    }

    public override async Task SaveAsync(CancellationToken token = default)
    {
        _staged[Current] = [.. Rows];

        // Only the layer that changed, so saving on one page never rewrites the other
        // file, and a file this could not read is left exactly as it is.
        var writing = Layers
            .Where(layer => Differs(layer) && !_unreadable.Contains(layer))
            .Select(layer => (Layer: layer, Entries: Entries(layer)))
            .ToArray();

        await Task.Run(
            () =>
            {
                foreach (var (layer, entries) in writing)
                {
                    _links.Write(_root, layer, entries);
                }
            },
            token).ConfigureAwait(true);
    }

    public override void Discard() => Fill();

    protected override WorkspaceLinkRowViewModel NewRow() =>
        Row(new WorkspaceLinkEntry(string.Empty, string.Empty, string.Empty));

    private SettingsLayer Current => _layer ?? SettingsLayer.TeamShared;

    /// <summary>What a layer would be written as, in the order its rows are drawn.</summary>
    private IReadOnlyList<WorkspaceLinkEntry> Entries(SettingsLayer layer)
    {
        var rows = layer == Current ? Rows.AsEnumerable() : _staged[layer];

        return [.. rows.Select(row => row.Entry)];
    }

    private bool Differs(SettingsLayer layer)
    {
        var rows = Entries(layer);
        var stored = _stored[layer];

        return rows.Count != stored.Count || rows.Where((row, index) => row != stored[index]).Any();
    }

    /// <summary>Reads both files. A layer that will not parse comes back as null.</summary>
    private IReadOnlyList<(SettingsLayer Layer, IReadOnlyList<WorkspaceLinkEntry>? Entries)> Read()
    {
        List<(SettingsLayer, IReadOnlyList<WorkspaceLinkEntry>?)> read = [];

        foreach (var layer in Layers)
        {
            try
            {
                read.Add((layer, _links.ReadEntries(_root, layer)));
            }
            catch (SettingsFileUnreadableException)
            {
                read.Add((layer, null));
            }
        }

        return read;
    }

    private void Fill()
    {
        foreach (var layer in Layers)
        {
            _staged[layer] = [.. _stored[layer].Select(Row)];
        }

        Show(Current);
    }

    private void Show(SettingsLayer layer)
    {
        Rows.Clear();

        foreach (var row in _staged[layer])
        {
            Rows.Add(row);
        }

        Announce();
    }

    private WorkspaceLinkRowViewModel Row(WorkspaceLinkEntry entry)
    {
        var icons = Icons;
        WorkspaceLinkIcon icon;

        if (entry.Icon.Length == 0)
        {
            icon = Icons.First(candidate => candidate.Glyph == IconGlyph.Link);
        }
        else if (_icons.TryRead(entry.Icon, out var glyph))
        {
            icon = Icons.First(candidate => candidate.Glyph == glyph);
        }
        else
        {
            // A name Kitbash does not draw is kept and offered back, so a save never
            // quietly rewrites it into something the file did not say.
            icon = new WorkspaceLinkIcon(IconGlyph.Link, entry.Icon);
            icons = [icon, .. Icons];
        }

        return new WorkspaceLinkRowViewModel(entry.Label, entry.Url, icon, entry.Icon, icons);
    }
}
