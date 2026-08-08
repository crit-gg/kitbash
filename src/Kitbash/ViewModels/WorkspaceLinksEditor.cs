using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;
using Kitbash.Workspaces;

namespace Kitbash.ViewModels;

/// <summary>
/// One workspace's links, edited in the settings window. It is its own editor because
/// <c>workspace.links</c> is an array of tables and no descriptor can describe one.
/// </summary>
public sealed partial class WorkspaceLinksEditor : ObservableObject, ISettingsEditor
{
    /// <summary>Both files are held at once, so moving the picker loses no staged work.</summary>
    private static readonly SettingsLayer[] Layers = [SettingsLayer.TeamShared, SettingsLayer.User];

    private readonly IWorkspaceLinks _links;
    private readonly WorkspaceLinkIcons _icons;
    private readonly string _root;

    private readonly Dictionary<SettingsLayer, ObservableCollection<WorkspaceLinkRowViewModel>> _rows =
        Layers.ToDictionary(layer => layer, _ => new ObservableCollection<WorkspaceLinkRowViewModel>());

    /// <summary>What each file said when it was last read. Dirty is measured against it.</summary>
    private readonly Dictionary<SettingsLayer, IReadOnlyList<WorkspaceLinkEntry>> _stored =
        Layers.ToDictionary(layer => layer, _ => (IReadOnlyList<WorkspaceLinkEntry>)[]);

    /// <summary>A layer whose file will not parse is never written over.</summary>
    private readonly HashSet<SettingsLayer> _unreadable = [];

    private SettingsLayer? _layer = SettingsLayer.TeamShared;

    [ObservableProperty]
    private bool _isPageWritable = true;

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

    /// <summary>The picked layer's own rows. The other layer's are held and not drawn.</summary>
    public ObservableCollection<WorkspaceLinkRowViewModel> Rows => _rows[Current];

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>
    /// Which file this page is pointed at. Moving it swaps what is drawn and nothing
    /// else, so anything staged on the layer being left is still there on the way back.
    /// </summary>
    public SettingsLayer? Layer
    {
        get => _layer;
        set
        {
            if (_layer == value)
            {
                return;
            }

            _layer = value;

            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsDirty => Layers.Any(Differs);

    /// <summary>
    /// A row that says nothing usable stops the save rather than being dropped, since
    /// dropping it would throw away what a person typed without saying so.
    /// </summary>
    public bool IsValid => Layers.All(layer => _rows[layer].All(row => row.IsValid));

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken token = default)
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

    public async Task SaveAsync(CancellationToken token = default)
    {
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

    public void Discard() => Fill();

    [RelayCommand]
    private void Add()
    {
        _rows[Current].Add(Row(new WorkspaceLinkEntry(string.Empty, string.Empty, string.Empty)));
        Announce();
    }

    partial void OnIsPageWritableChanged(bool value)
    {
        foreach (var row in Layers.SelectMany(layer => _rows[layer]))
        {
            row.CanEdit = value;
        }
    }

    private SettingsLayer Current => _layer ?? SettingsLayer.TeamShared;

    private IReadOnlyList<WorkspaceLinkEntry> Entries(SettingsLayer layer) =>
        [.. _rows[layer].Select(row => row.Entry)];

    private bool Differs(SettingsLayer layer)
    {
        var rows = _rows[layer];
        var stored = _stored[layer];

        return rows.Count != stored.Count
            || rows.Where((row, index) => row.Entry != stored[index]).Any();
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
            _rows[layer].Clear();

            foreach (var entry in _stored[layer])
            {
                _rows[layer].Add(Row(entry));
            }
        }

        OnPropertyChanged(nameof(Rows));
        Announce();
    }

    private WorkspaceLinkRowViewModel Row(WorkspaceLinkEntry entry)
    {
        var icons = Icons;
        var icon = Icons[0];

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

        return new WorkspaceLinkRowViewModel(
            entry.Label,
            entry.Url,
            icon,
            entry.Icon,
            icons,
            Announce,
            row =>
            {
                _rows[Current].Remove(row);
                Announce();
            })
        {
            CanEdit = IsPageWritable,
        };
    }

    private void Announce()
    {
        OnPropertyChanged(nameof(IsEmpty));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
