using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Ui.Settings;

/// <summary>A group of rows under one heading.</summary>
public sealed class SettingsSectionViewModel
{
    public SettingsSectionViewModel(string title, IReadOnlyList<SettingsRowViewModel> rows)
    {
        Title = title.ToUpperInvariant();
        Rows = rows;
    }

    public string Title { get; }

    public IReadOnlyList<SettingsRowViewModel> Rows { get; }
}

/// <summary>One file behind a page, as the backing table draws it.</summary>
public sealed class SettingsFileViewModel
{
    public SettingsFileViewModel(SettingsFileView file, string path)
    {
        Layer = file.Layer switch
        {
            SettingsLayer.TeamShared => "TEAM",
            SettingsLayer.User => "USER",
            _ => "FILE",
        };

        Path = path;
        IsUnreadable = file.ParseError is not null;
        IsMissing = !file.Exists;
        Status = IsUnreadable ? "unreadable" : file.Exists ? "ok" : "missing";
    }

    public string Layer { get; }

    public string Path { get; }

    public string Status { get; }

    public bool IsMissing { get; }

    public bool IsUnreadable { get; }
}

/// <summary>
/// One settings page, read off disk and drawn. It holds the unsaved changes for its own
/// rows, so switching pages and coming back keeps them.
/// </summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    /// <summary>
    /// Long enough that this only folds the home directory and writes the platform's own
    /// separator. What fits the column is TextTrimming's job, in rendered width.
    /// </summary>
    private const int PathWidth = 96;

    private readonly SettingsPage _page;
    private readonly SettingsPlace? _place;
    private readonly SettingsScope _scope;
    private readonly ISettingsInspector _inspector;
    private readonly ISettingsWriter _writer;
    private readonly ISettingsValueConverter _converter;
    private readonly IPathShortener _shortener;
    private readonly Action _changed;

    private readonly List<SettingValueRowViewModel> _values = [];
    private readonly List<(SettingsReadoutRow Source, SettingsReadoutRowViewModel Row)> _readouts = [];
    private readonly List<ISettingsEditor> _editors = [];

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private SettingsLayer _layer = SettingsLayer.User;

    private SettingsPageView? _view;
    private bool _restartWasSaved;

    public SettingsPageViewModel(
        SettingsPage page,
        SettingsPlace? place,
        SettingsScope scope,
        ISettingsInspector inspector,
        ISettingsWriter writer,
        ISettingsValueConverter converter,
        IPathShortener shortener,
        Action changed)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(shortener);
        ArgumentNullException.ThrowIfNull(changed);

        _page = page;
        _place = place;
        _scope = scope;
        _inspector = inspector;
        _writer = writer;
        _converter = converter;
        _shortener = shortener;
        _changed = changed;

        Sections = Build(page);
    }

    public string Title => _page.Title;

    public string ScopeLine => Describe(_page, _place, _scope);

    public IReadOnlyList<SettingsSectionViewModel> Sections { get; }

    public ObservableCollection<SettingsFileViewModel> Files { get; } = [];

    public bool IsLayered => _page.IsLayered;

    /// <summary>
    /// The two ends of the layer picker. Each writes rather than toggles, so unchecking
    /// one by picking the other never leaves the page pointing at neither.
    /// </summary>
    public bool IsUser
    {
        get => Layer is SettingsLayer.User;
        set
        {
            if (value)
            {
                Layer = SettingsLayer.User;
            }
        }
    }

    /// <inheritdoc cref="IsUser"/>
    public bool IsTeam
    {
        get => Layer is SettingsLayer.TeamShared;
        set
        {
            if (value)
            {
                Layer = SettingsLayer.TeamShared;
            }
        }
    }

    /// <summary>The picker points at a file, so it goes when the page has none.</summary>
    public bool ShowsLayerPicker => IsLayered && IsAvailable;

    /// <summary>Team shared is committed to git, so choosing it is worth saying out loud.</summary>
    public bool ShowsTeamWarning => IsLayered && IsTeam;

    /// <summary>Which file a change on this page would land in.</summary>
    public string LayerFile => Shorten(File(Layer)?.Path);

    /// <summary>
    /// A layer whose file will not parse cannot be written, since writing it would
    /// replace content this app could not read.
    /// </summary>
    public bool CanUseTeam => Writable(SettingsLayer.TeamShared);

    public bool CanUseUser => Writable(SettingsLayer.User);

    public bool IsWritable => (_view?.IsWritable ?? false) && Writable(IsLayered ? Layer : null);

    public bool HasParseError => _view?.HasParseError ?? false;

    /// <summary>What is unreadable and what still works, in one sentence.</summary>
    public string ParseErrorMessage => Broken() switch
    {
        [] => string.Empty,
        [{ Layer: SettingsLayer.TeamShared }] =>
            "The team file is not valid TOML, so nothing it holds can be read. Your personal settings still work.",
        [{ Layer: SettingsLayer.User }] =>
            "Your personal file is not valid TOML, so nothing it holds can be read. The team settings still work.",
        _ => "This file is not valid TOML, so nothing on this page can be read.",
    };

    public string ParseErrorFiles => string.Join(", ", Broken().Select(file => Shorten(file.Path)));

    public string EmptyMessage =>
        _page.Home is SettingsHome.Workspace
            ? "There is no workspace behind this page any more."
            : "There is nowhere to keep these settings on this machine.";

    /// <summary>An editor counts once however much is staged inside it.</summary>
    public int DirtyCount => _values.Count(row => row.IsDirty) + _editors.Count(editor => editor.IsDirty);

    public bool IsDirty => DirtyCount > 0;

    /// <summary>Every unsaved change on this page can be written as it stands.</summary>
    public bool IsValid => _values.All(row => row.IsValid) && _editors.All(editor => editor.IsValid);

    /// <summary>
    /// A restart is owed, either by an unsaved change or by one this window already wrote.
    /// The second half is why it is remembered rather than worked out from the rows: after
    /// a save there is no unsaved change left to read it from.
    /// </summary>
    public bool NeedsRestart => _restartWasSaved || _values.Any(row => row.IsRestartStaged);

    public void Discard()
    {
        foreach (var row in _values)
        {
            row.Discard();
        }

        foreach (var editor in _editors)
        {
            editor.Discard();
        }
    }

    /// <summary>Reads every file behind the page. Touches a disk, so it stays off the UI thread.</summary>
    public async Task LoadAsync(CancellationToken token = default)
    {
        var load = await Task.Run(() => ReadAsync(token), token).ConfigureAwait(true);

        _view = load.View;
        IsAvailable = load.View.IsAvailable;

        Files.Clear();

        foreach (var file in load.View.Files)
        {
            Files.Add(new SettingsFileViewModel(file, Shorten(file.Path)));
        }

        // A layer that cannot be written is not the one to be pointed at, so the picker
        // moves off it rather than offering a save that would be refused.
        if (IsLayered && !Writable(Layer) && Writable(Other(Layer)))
        {
            Layer = Other(Layer);
        }

        foreach (var row in _values)
        {
            if (load.Choices.TryGetValue(row.Key, out var choices))
            {
                row.Offer(choices);
            }

            if (load.View.Find(row.Key) is { } value)
            {
                row.Apply(value);
            }

            row.IsPageWritable = IsWritable;
        }

        foreach (var (row, entries) in load.Readouts)
        {
            row.Entries = entries;
        }

        // An editor owns its own file, so it reads itself. It runs here rather than with
        // the page read above, since what it fills is bound to the window.
        foreach (var editor in _editors)
        {
            editor.IsPageWritable = IsWritable;
            await editor.LoadAsync(token).ConfigureAwait(true);
        }

        Announce();
    }

    /// <summary>
    /// Writes every unsaved change on this page at once, then reads the files again so
    /// what is drawn is what landed.
    /// </summary>
    public async Task SaveAsync(CancellationToken token = default)
    {
        var edits = _values.Select(row => row.Edit()).OfType<SettingsEdit>().ToArray();
        var staged = _editors.Where(editor => editor.IsDirty).ToArray();

        if (edits.Length == 0 && staged.Length == 0)
        {
            return;
        }

        var layer = IsLayered ? Layer : (SettingsLayer?)null;
        var restarts = _values.Any(row => row.IsRestartStaged);

        if (edits.Length > 0)
        {
            await Task.Run(() => _writer.Write(_scope, _page, _place, layer, edits), token).ConfigureAwait(true);
        }

        foreach (var editor in staged)
        {
            await editor.SaveAsync(token).ConfigureAwait(true);
        }

        _restartWasSaved |= restarts;
        Discard();
        await LoadAsync(token).ConfigureAwait(true);
    }

    partial void OnLayerChanged(SettingsLayer value)
    {
        foreach (var row in _values)
        {
            row.IsPageWritable = IsWritable;
        }

        foreach (var editor in _editors)
        {
            editor.IsPageWritable = IsWritable;
        }

        Announce();
    }

    private IReadOnlyList<SettingsSectionViewModel> Build(SettingsPage page)
    {
        var sections = new List<SettingsSectionViewModel>(page.Sections.Count);

        foreach (var section in page.Sections)
        {
            var rows = new List<SettingsRowViewModel>(section.Rows.Count);

            foreach (var row in section.Rows)
            {
                switch (row)
                {
                    case ISettingDescriptor descriptor:
                        var value = new SettingValueRowViewModel(descriptor, _converter, OnRowChanged);
                        _values.Add(value);
                        rows.Add(value);
                        break;

                    case SettingsReadoutRow readout:
                        var reading = new SettingsReadoutRowViewModel(readout);
                        _readouts.Add((readout, reading));
                        rows.Add(reading);
                        break;

                    case SettingsEditorRow editor:
                        editor.Editor.Changed += OnEditorChanged;
                        _editors.Add(editor.Editor);
                        rows.Add(new SettingsEditorRowViewModel(editor));
                        break;
                }
            }

            sections.Add(new SettingsSectionViewModel(section.Title, rows));
        }

        return sections;
    }

    private async Task<PageLoad> ReadAsync(CancellationToken token)
    {
        var view = _inspector.Read(_scope, _page, _place);
        var choices = new Dictionary<string, IReadOnlyList<SettingChoice>>(StringComparer.Ordinal);

        foreach (var descriptor in _page.Descriptors)
        {
            if (descriptor.Editor is SettingEditor.Select or SettingEditor.Segment)
            {
                choices[descriptor.Key] = await descriptor.Offer(token).ConfigureAwait(false);
            }
        }

        var readouts = _readouts
            .Select(pair => (pair.Row, Entries: pair.Source.Read()))
            .ToArray();

        return new PageLoad(view, choices, readouts);
    }

    private void OnEditorChanged(object? sender, EventArgs args) => OnRowChanged();

    private void OnRowChanged()
    {
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(NeedsRestart));
        _changed();
    }

    private SettingsFileView? File(SettingsLayer layer) =>
        _view?.Files.FirstOrDefault(file => file.Layer == layer)
        ?? _view?.Files.FirstOrDefault(file => file.Layer is null);

    private bool Writable(SettingsLayer? layer) =>
        (layer is { } chosen ? File(chosen) : _view?.Files.FirstOrDefault())?.CanBeWritten ?? false;

    private IReadOnlyList<SettingsFileView> Broken() =>
        _view is null ? [] : [.. _view.Files.Where(file => file.ParseError is not null)];

    private string Shorten(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : _shortener.Shorten(path, PathWidth);

    /// <summary>Everything here is worked out from the page view, so one word covers it.</summary>
    private void Announce() => OnPropertyChanged(string.Empty);

    private static SettingsLayer Other(SettingsLayer layer) =>
        layer is SettingsLayer.User ? SettingsLayer.TeamShared : SettingsLayer.User;

    /// <summary>
    /// Where this page writes, in the words the tree used to get here. A named place goes
    /// in it, since two pages under two workspaces are otherwise the same page twice.
    /// </summary>
    private static string Describe(SettingsPage page, SettingsPlace? place, SettingsScope scope)
    {
        var file = scope.IsGlobal ? "kitbash.toml" : $"tools/{scope.ToolId}.toml";
        var named = place is { IsNamed: true } ? $" {place.Name}" : string.Empty;

        return page.Home switch
        {
            SettingsHome.State => "application state / read only",
            SettingsHome.Workspace => $"workspace{named} / {file}",
            _ => $"application{named} / {file}",
        };
    }

    private sealed record PageLoad(
        SettingsPageView View,
        IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>> Choices,
        IReadOnlyList<(SettingsReadoutRowViewModel Row, IReadOnlyList<SettingsListEntry> Entries)> Readouts);
}
