using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Settings;

/// <summary>
/// One app's settings window. It draws whatever schema it is given, so the launcher and
/// every tool share this and differ only in the pages they hand it.
/// </summary>
public sealed partial class SettingsWindowViewModel : ObservableObject
{
    private readonly SettingsSchema _schema;
    private readonly ISettingsInspector _inspector;
    private readonly ISettingsWriter _writer;
    private readonly ISettingsValueConverter _converter;
    private readonly IPathShortener _shortener;
    private readonly IApplicationRestart _restart;

    private readonly Dictionary<string, SettingsPageViewModel> _pages = new(StringComparer.Ordinal);

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private SettingsPageViewModel? _page;

    [ObservableProperty]
    private string _problem = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    public SettingsWindowViewModel(
        SettingsSchema schema,
        ISettingsInspector inspector,
        ISettingsWriter writer,
        ISettingsValueConverter converter,
        IPathShortener shortener,
        IApplicationRestart restart)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(shortener);
        ArgumentNullException.ThrowIfNull(restart);

        _schema = schema;
        _inspector = inspector;
        _writer = writer;
        _converter = converter;
        _shortener = shortener;
        _restart = restart;

        Rows = new TreeRows(Roots(), node => (node as SettingsTreeNode)?.Children);
        Open();
    }

    public string Title => _schema.Title;

    /// <summary>The tree, flattened to the rows that can be seen.</summary>
    public TreeRows Rows { get; }

    public bool HasProblem => Problem.Length > 0;

    public int DirtyCount => _pages.Values.Sum(page => page.DirtyCount);

    public bool IsDirty => DirtyCount > 0;

    public string DirtyLabel =>
        DirtyCount == 1 ? "1 unsaved change" : $"{DirtyCount} unsaved changes";

    /// <summary>
    /// Everything staged can be written as it stands. A value a rule refuses is kept on
    /// the row and stops the save rather than being thrown away as it is typed.
    /// </summary>
    public bool CanSave => IsDirty && !IsSaving && _pages.Values.All(page => page.IsValid);

    public bool CanDiscard => IsDirty && !IsSaving;

    /// <summary>
    /// Something here only means anything once the app starts again, either staged or
    /// already written by this window.
    /// </summary>
    public bool NeedsRestart => _pages.Values.Any(page => page.NeedsRestart);

    /// <summary>Saving first when there is anything to save, which there usually is.</summary>
    public string RestartLabel => IsDirty ? "Save and restart" : "Restart now";

    public bool CanRestart => NeedsRestart && !IsSaving && (!IsDirty || CanSave);

    /// <summary>Opens a page, reading its files the first time it is asked for.</summary>
    public async Task OpenAsync(SettingsTreeNode? node)
    {
        if (node is null || node.Page is not { } page || node.Place is not { } place)
        {
            return;
        }

        if (!_pages.TryGetValue(node.Key, out var model))
        {
            model = new SettingsPageViewModel(
                page, place, _schema.Scope, _inspector, _writer, _converter, _shortener, OnPageChanged);

            _pages[node.Key] = model;
            Page = model;

            await Guarded(model.LoadAsync()).ConfigureAwait(true);
            return;
        }

        Page = model;
    }

    /// <summary>
    /// Builds the tree again, since nothing watches the workspace list. Separate from
    /// <see cref="ReloadAsync"/> and synchronous, so a caller can put the selection back
    /// before anything else runs and never leave it pointing at a row that moved.
    /// </summary>
    public void Refresh()
    {
        Rows.Reset(Roots());
        Open();
    }

    /// <summary>
    /// Reads every page that has been opened again. Nothing watches the filesystem, so
    /// this runs when the window comes to the front.
    /// </summary>
    public async Task ReloadAsync()
    {
        foreach (var page in _pages.Values.ToArray())
        {
            await Guarded(page.LoadAsync()).ConfigureAwait(true);
        }
    }

    /// <summary>The first page in the tree, which is what a window opens on.</summary>
    public SettingsTreeNode? First() =>
        Rows.Select(row => row.Item).OfType<SettingsTreeNode>().FirstOrDefault(node => !node.IsGroup);

    /// <summary>
    /// Where a node sits in the tree right now, or minus one when it is not shown. By key
    /// rather than by reference, since the tree is built again whenever it is reloaded.
    /// </summary>
    public int IndexOf(SettingsTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        for (var index = 0; index < Rows.Count; index++)
        {
            if (Rows[index].Item is SettingsTreeNode candidate
                && string.Equals(candidate.Key, node.Key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        IsSaving = true;
        Problem = string.Empty;

        try
        {
            foreach (var page in _pages.Values.Where(page => page.IsDirty).ToArray())
            {
                await Guarded(page.SaveAsync()).ConfigureAwait(true);
            }
        }
        finally
        {
            IsSaving = false;
            OnPageChanged();
        }
    }

    /// <summary>
    /// Writes whatever is staged, then starts again. A save that did not land leaves the
    /// changes where they are and nothing restarts over the top of them.
    /// </summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (!CanRestart)
        {
            return;
        }

        if (IsDirty)
        {
            await SaveAsync().ConfigureAwait(true);

            if (IsDirty)
            {
                return;
            }
        }

        if (!_restart.Restart())
        {
            Problem = "A new copy could not be started, so nothing has restarted.";
        }
    }

    [RelayCommand]
    private void Discard()
    {
        foreach (var page in _pages.Values)
        {
            page.Discard();
        }

        Problem = string.Empty;
    }

    partial void OnSearchChanged(string value)
    {
        Rows.Reset(Roots());
        Open();
    }

    /// <summary>
    /// Every store stands open. There are three of them and each holds a handful of
    /// pages, so a closed one hides work rather than saving room.
    /// </summary>
    private void Open()
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            var row = Rows[index];

            if (row.HasChildren && !row.IsExpanded)
            {
                Rows.Expand(row);
            }
        }
    }

    partial void OnProblemChanged(string value) => OnPropertyChanged(nameof(HasProblem));

    partial void OnIsSavingChanged(bool value) => OnPageChanged();

    /// <summary>
    /// A disk can refuse a read or a write at any point, and a file that will not parse
    /// refuses the write on purpose. None of those may take the window down.
    /// </summary>
    private async Task Guarded(Task work)
    {
        try
        {
            await work.ConfigureAwait(true);
        }
        catch (SettingsFileUnreadableException exception)
        {
            Problem = exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Problem = "Kitbash could not read or write a settings file on this machine.";
        }
    }

    private void OnPageChanged()
    {
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(DirtyLabel));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanDiscard));
        OnPropertyChanged(nameof(NeedsRestart));
        OnPropertyChanged(nameof(RestartLabel));
        OnPropertyChanged(nameof(CanRestart));
    }

    /// <summary>
    /// The stores, each holding the pages that stand on it. A store with no page left
    /// after a search is left out, so filtering never leaves an empty heading behind, and
    /// so is one with nowhere to keep anything. A store whose places are named holds a
    /// level of those, with the same pages under each.
    /// </summary>
    private IReadOnlyList<SettingsTreeNode> Roots()
    {
        var words = Search.Trim();
        var roots = new List<SettingsTreeNode>();

        foreach (var home in Enum.GetValues<SettingsHome>())
        {
            var pages = _schema
                .PagesIn(home)
                .Where(page => words.Length == 0 || Matches(page, words))
                .ToArray();

            var places = _inspector.PlacesIn(home);

            if (pages.Length == 0 || places.Count == 0)
            {
                continue;
            }

            roots.Add(SettingsTreeNode.Store(Word(home), Under(pages, places)));
        }

        return roots;
    }

    private static IReadOnlyList<SettingsTreeNode> Under(
        IReadOnlyList<SettingsPage> pages,
        IReadOnlyList<SettingsPlace> places)
    {
        // One unnamed place is a store with only one of whatever it holds, so there is no
        // level to draw and the pages hang straight off it.
        if (places is [{ IsNamed: false } only])
        {
            return [.. pages.Select(page => SettingsTreeNode.For(page, only))];
        }

        return
        [
            .. places.Select(place => SettingsTreeNode.For(
                place,
                [.. pages.Select(page => SettingsTreeNode.For(page, place))])),
        ];
    }

    /// <summary>
    /// Whether a page answers to the words a person typed. Its title, and every row's
    /// name, description and key. The schema alone, so searching opens nothing.
    /// </summary>
    private static bool Matches(SettingsPage page, string words)
    {
        if (page.Title.Contains(words, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var section in page.Sections)
        {
            foreach (var row in section.Rows)
            {
                if (row.Name.Contains(words, StringComparison.OrdinalIgnoreCase)
                    || row.Description.Contains(words, StringComparison.OrdinalIgnoreCase)
                    || (row is ISettingDescriptor setting
                        && setting.Key.Contains(words, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Word(SettingsHome home) => home switch
    {
        SettingsHome.Workspace => "Workspace",
        SettingsHome.State => "State",
        _ => "Application",
    };
}
