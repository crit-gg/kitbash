using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Projects;

namespace Kitbash.Ui.Projects;

/// <summary>
/// The list, what is being looked for in it and what order it is in. Everything that
/// needs a window to open something over belongs to the window instead.
/// </summary>
public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly IRecentProjects _recent;
    private readonly IProjectKind _kind;

    /// <summary>One load at a time. Two overlapping race on what the store holds.</summary>
    private readonly SemaphoreSlim _loading = new(1, 1);

    private IReadOnlyList<ProjectRowViewModel> _all = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private ProjectSort _sort = ProjectSort.Recent;

    public ProjectsViewModel(IRecentProjects recent, IProjectKind kind)
    {
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(kind);

        _recent = recent;
        _kind = kind;

        // The first read is synchronous, since it runs before the window is on screen.
        // There is no frame to drop and the window opens filled in.
        Apply(Read());
    }

    public ProjectWords Words => _kind.Words;

    /// <summary>What is drawn, which is the whole list filtered and ordered.</summary>
    public ObservableCollection<ProjectRowViewModel> Rows { get; } = [];

    /// <summary>Nothing has ever been opened, so there is no list to search.</summary>
    public bool HasNone => _all.Count == 0;

    public bool HasAny => !HasNone;

    /// <summary>There is a list and the search matched none of it.</summary>
    public bool IsEmpty => HasAny && Rows.Count == 0;

    public string EmptyText => $"Nothing matches {Search.Trim()}";

    /// <summary>
    /// What the list is, or how much of it is left. Upper case here rather than in the
    /// theme, since the section label draws whatever string it is handed.
    /// </summary>
    public string ListHeading =>
        Query.Length == 0
            ? Upper($"Recent {Words.Items}")
            : Upper($"{Rows.Count} of {_all.Count} {Words.Items}");

    public string SortLabel => Sort switch
    {
        ProjectSort.Name => "Name",
        ProjectSort.Path => "Path",
        _ => "Recently opened",
    };

    /// <summary>
    /// Which row in the sort menu carries the mark. The menu has no toggle of its own, so
    /// the answer is a glyph the item is handed.
    /// </summary>
    public bool IsByRecent => Sort == ProjectSort.Recent;

    public bool IsByName => Sort == ProjectSort.Name;

    public bool IsByPath => Sort == ProjectSort.Path;

    /// <summary>The rail item's word. The plural, as a label rather than a sentence.</summary>
    public string ListLabel =>
        Words.Items.Length == 0
            ? Words.Items
            : char.ToUpper(Words.Items[0], CultureInfo.CurrentCulture) + Words.Items[1..];

    private string Query => Search.Trim();

    /// <summary>Reads the store and the app's answer for every row, off the UI thread.</summary>
    public async Task LoadAsync()
    {
        await _loading.WaitAsync().ConfigureAwait(true);

        try
        {
            Apply(await Task.Run(Read).ConfigureAwait(true));
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>Puts a path at the front and stamps it, which is what opening one does.</summary>
    public RecentProject Remember(ProjectChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        return _recent.Remember(choice.Path, choice.Name);
    }

    public void Forget(string path) => _recent.Forget(path);

    [RelayCommand]
    private void SortBy(ProjectSort sort) => Sort = sort;

    partial void OnSearchChanged(string value) => Refilter();

    partial void OnSortChanged(ProjectSort value)
    {
        Refilter();

        OnPropertyChanged(nameof(SortLabel));
        OnPropertyChanged(nameof(IsByRecent));
        OnPropertyChanged(nameof(IsByName));
        OnPropertyChanged(nameof(IsByPath));
    }

    private IReadOnlyList<ProjectRowViewModel> Read()
    {
        _recent.Refresh();

        var now = DateTimeOffset.Now;

        return [.. _recent.All.Select(project => new ProjectRowViewModel(
            project,
            Facts(project),
            RecentTime.Describe(project.LastOpened, now)))];
    }

    // An app answering badly costs that row its chip rather than costing the window its
    // list, since the list is the only way back to anything.
    private ProjectFacts Facts(RecentProject project)
    {
        try
        {
            return _kind.Describe(project) ?? new ProjectFacts();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ProjectFacts { IsBroken = true };
        }
    }

    private void Apply(IReadOnlyList<ProjectRowViewModel> rows)
    {
        _all = rows;

        Refilter();

        OnPropertyChanged(nameof(HasNone));
        OnPropertyChanged(nameof(HasAny));
    }

    private void Refilter()
    {
        var query = Query;

        IReadOnlyList<ProjectRowViewModel> shown = query.Length == 0
            ? _all
            : [.. _all.Where(row => row.Matches(query))];

        // Filtering keeps the order that was chosen, so typing never rearranges what is
        // left. Recent is the order the store already holds.
        IEnumerable<ProjectRowViewModel> ordered = Sort switch
        {
            ProjectSort.Name => shown.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase),
            ProjectSort.Path => shown.OrderBy(row => row.Path, StringComparer.CurrentCultureIgnoreCase),
            _ => shown,
        };

        Rows.Clear();

        foreach (var row in ordered)
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(ListHeading));
    }

    private static string Upper(string text) => text.ToUpper(CultureInfo.CurrentCulture);
}
