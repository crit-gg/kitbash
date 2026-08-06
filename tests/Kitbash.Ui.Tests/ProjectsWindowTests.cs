using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Core.Projects;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Projects;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The welcome window, drawn for real with no display. What matters is that the app owns
/// what a project is and the window owns the list.
/// </summary>
public class ProjectsWindowTests
{
    [AvaloniaFact]
    public void EveryRowSaysNamePathStateAndWhen()
    {
        var window = Open(Kind());

        var rows = List(window);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["Slopworks", "Heat rebalance spike", "Ashfall prototype"],
            rows.Select(row => row.Name));

        var first = rows[0];

        Assert.Equal("S", first.Mark);
        Assert.Equal("GODOT 4.7.1", first.Chip);
        Assert.True(first.HasChip);
        Assert.Equal("just now", first.Time);
    }

    [AvaloniaFact]
    public void TheHeadingCountsWhatIsLeftWhileSearchingAndNamesTheListOtherwise()
    {
        var window = Open(Kind());
        var model = Model(window);

        Assert.Equal("RECENT PROJECTS", model.ListHeading);

        model.Search = "heat";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 OF 3 PROJECTS", model.ListHeading);
        Assert.Equal(["Heat rebalance spike"], model.Rows.Select(row => row.Name));
    }

    // A path is searched as well as a name, since half of what tells two rows apart is
    // the folder they are in.
    [AvaloniaFact]
    public void SearchingReadsThePathToo()
    {
        var model = Model(Open(Kind()));

        model.Search = "spikes";

        Assert.Equal(["Heat rebalance spike"], model.Rows.Select(row => row.Name));
    }

    [AvaloniaFact]
    public void SearchingFiltersAndNeverSorts()
    {
        var model = Model(Open(Kind()));

        model.Sort = ProjectSort.Name;

        Assert.Equal(["Ashfall prototype", "Heat rebalance spike", "Slopworks"],
            model.Rows.Select(row => row.Name));

        model.Search = "o";

        // Still in name order rather than back in the order the store holds.
        Assert.Equal(["Ashfall prototype", "Slopworks"], model.Rows.Select(row => row.Name));
        Assert.Equal(ProjectSort.Name, model.Sort);
    }

    [AvaloniaFact]
    public void NothingMatchingSaysSoWithoutEmptyingTheWindow()
    {
        var window = Open(Kind());
        var model = Model(window);

        model.Search = "nothing like this";
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(model.IsEmpty);
        Assert.False(model.HasNone);
        Assert.Equal("Nothing matches nothing like this", model.EmptyText);

        // The action row is still there, so there is still a way to open something.
        Assert.True(window.GetControl<SearchBox>("Query").IsVisible);
    }

    [AvaloniaFact]
    public void AListThatHasNeverHeldAnythingIsAPageOfItsOwn()
    {
        var kind = new FakeKind([]);
        var model = Model(Open(kind));

        Assert.True(model.HasNone);
        Assert.False(model.HasAny);
        Assert.Equal("No projects yet", model.Words.EmptyTitle);
    }

    // A greyed row reads as unimportant when it is the one needing a decision.
    [AvaloniaFact]
    public void ARowThatCannotBeReachedKeepsItsNameAndOffersToBeLocated()
    {
        var rows = List(Open(Kind()));
        var broken = rows.Single(row => row.Name == "Ashfall prototype");

        Assert.True(broken.IsBroken);
        Assert.Equal("Locate the folder", broken.OpenLabel);
        Assert.Equal("NOT FOUND", broken.Chip);
        Assert.True(broken.HasChipGlyph);

        Assert.Equal("Open", rows[0].OpenLabel);
        Assert.False(rows[0].HasChipGlyph);
    }

    // A path that is not on disk is broken whatever the app said about it.
    [AvaloniaFact]
    public void AMissingPathIsBrokenWithoutTheAppSayingSo()
    {
        var kind = new FakeKind(
        [
            new RecentProject("/gone", "Gone", DateTimeOffset.UtcNow, Exists: false),
        ]);

        Assert.True(List(Open(kind)).Single().IsBroken);
    }

    [AvaloniaFact]
    public async Task OpeningARowRemembersItAndTellsTheApp()
    {
        var kind = Kind();
        var window = Open(kind);

        await window.OpenAsync(List(window)[1], inNewWindow: false);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Heat rebalance spike"], kind.Opened);
        Assert.Equal("Heat rebalance spike", kind.Recent.All[0].Name);
    }

    // The row menu offers the only thing that would help, and so does a double click.
    [AvaloniaFact]
    public async Task OpeningABrokenRowBrowsesForItInstead()
    {
        var kind = Kind();
        var window = Open(kind);

        await window.OpenAsync(List(window).Single(row => row.IsBroken), inNewWindow: false);

        Dispatcher.UIThread.RunJobs();

        Assert.Empty(kind.Opened);
        Assert.Equal(["Ashfall prototype"], kind.Located);
    }

    [AvaloniaFact]
    public void AnAppThatAddedNoPagesHasTheListAlone()
    {
        var window = Open(Kind());

        var rail = window.GetControl<ListBox>("Rail");

        Assert.Single(rail.Items);
        Assert.True(window.GetControl<Panel>("ListPage").IsVisible);
        Assert.False(window.GetControl<ContentControl>("PagePane").IsVisible);

        // Built in code, so the rail item theme is looked up rather than named in markup.
        // A miss would draw Fluent's own list row at whatever height it likes.
        var item = (ListBoxItem)rail.ContainerFromIndex(0)!;

        Assert.Equal(32, item.Bounds.Width);
        Assert.Equal(32, item.Bounds.Height);
    }

    [AvaloniaFact]
    public void APageTheAppAddedSitsUnderTheListAndTakesThePane()
    {
        var page = new TextBlock { Text = "The app's own page" };

        var kind = new FakeKind([])
        {
            Pages = [new ProjectPage(IconGlyph.GitBranch, "Remotes", page)],
        };

        var window = Open(kind);
        var rail = window.GetControl<ListBox>("Rail");

        Assert.Equal(2, rail.Items.Count);
        Assert.Equal(0, rail.SelectedIndex);

        rail.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var pane = window.GetControl<ContentControl>("PagePane");

        Assert.True(pane.IsVisible);
        Assert.Same(page, pane.Content);
        Assert.False(window.GetControl<Panel>("ListPage").IsVisible);

        // The list is still there to come back to, and the page it left is the same one.
        rail.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.GetControl<Panel>("ListPage").IsVisible);
        Assert.False(pane.IsVisible);
    }

    [AvaloniaFact]
    public void AnAppWithNoSettingsWindowDrawsNoCog()
    {
        var window = Open(Kind());

        Assert.False(window.GetControl<ListBox>("RailFoot").IsVisible);
        Assert.True(window.GetControl<ListBox>("Rail").IsVisible);
    }

    [AvaloniaFact]
    public void TheRailAndTheTitleSayWhichAppThisIs()
    {
        var model = Model(Open(Kind()));

        Assert.Equal("Welcome to Foundry", model.Words.Title);
        Assert.Equal("Projects", model.ListLabel);
    }

    // An app answering badly costs that row its chip rather than the window its list.
    [AvaloniaFact]
    public void AnAppThatThrowsWhileDescribingARowStillLeavesTheRowThere()
    {
        var kind = new FakeKind(
        [
            new RecentProject("/one", "One", DateTimeOffset.UtcNow, Exists: true),
        ])
        {
            Throws = true,
        };

        var rows = List(Open(kind));

        Assert.Single(rows);
        Assert.Equal("One", rows[0].Name);
        Assert.True(rows[0].IsBroken);
    }

    // So Enter opens the thing a person came back for, and the keyboard has a start.
    [AvaloniaFact]
    public void TheFirstRowIsPickedWhenTheWindowOpens()
    {
        var window = Open(Kind());

        Assert.Equal(0, window.GetControl<ListBox>("Projects").SelectedIndex);

        // Nothing to pick, so nothing is picked rather than an index nothing answers.
        var bare = Open(new FakeKind([]));

        Assert.Equal(-1, bare.GetControl<ListBox>("Projects").SelectedIndex);
    }

    // The row carries two lines, so it takes its own height rather than the list's, and
    // the tile at the head of it is the badge tiers at a size a list can be run down.
    [AvaloniaFact]
    public void ARowIsTheListRowAtItsOwnHeight()
    {
        var window = Open(Kind());

        var containers = window.GetControl<ListBox>("Projects")
            .GetRealizedContainers()
            .OfType<ListBoxItem>()
            .ToList();

        Assert.Equal(3, containers.Count);
        Assert.Equal(48, containers[0].Bounds.Height);

        var tile = containers[0].GetVisualDescendants().OfType<Badge>().First();

        Assert.Contains("tile", tile.Classes);
        Assert.Equal(32, tile.Bounds.Width);
        Assert.Equal(32, tile.Bounds.Height);
    }

    private static IReadOnlyList<ProjectRowViewModel> List(ProjectsWindow window) =>
        Model(window).Rows;

    private static ProjectsViewModel Model(ProjectsWindow window) =>
        (ProjectsViewModel)window.DataContext!;

    private static FakeKind Kind()
    {
        var now = DateTimeOffset.UtcNow;

        return new FakeKind(
        [
            new RecentProject("D:/dev/slopworks/godot", "Slopworks", now, Exists: true),
            new RecentProject(
                "D:/dev/spikes/heat-rebalance", "Heat rebalance spike", now.AddDays(-3), true),
            new RecentProject("D:/dev/ashfall", "Ashfall prototype", now.AddDays(-40), false),
        ]);
    }

    private static ProjectsWindow Open(FakeKind kind)
    {
        var window = new ProjectsWindow
        {
            Kind = kind,
            DataContext = new ProjectsViewModel(kind.Recent, kind),
        };

        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return window;
    }
}

/// <summary>An app's half of the seam, recording what the window asked it to do.</summary>
internal sealed class FakeKind : IProjectKind
{
    private static readonly Dictionary<string, ProjectFacts> Facts = new(StringComparer.Ordinal)
    {
        ["D:/dev/slopworks/godot"] = new()
        {
            Chip = "GODOT 4.7.1",
            ChipTier = BadgeTier.Accent,
        },
        ["D:/dev/spikes/heat-rebalance"] = new()
        {
            Chip = "GODOT 4.8 BETA",
            ChipTier = BadgeTier.Graph,
        },
        ["D:/dev/ashfall"] = new()
        {
            Chip = "NOT FOUND",
            ChipTier = BadgeTier.Error,
            ChipGlyph = IconGlyph.AlertCircle,
            IsBroken = true,
        },
    };

    public FakeKind(IEnumerable<RecentProject> seed)
    {
        Recent = new FakeRecent(seed);
    }

    public ProjectWords Words { get; } = new("Foundry", "project", "projects");

    public bool ClosesOnOpen { get; init; }

    public IReadOnlyList<ProjectPage> Pages { get; init; } = [];

    public bool Throws { get; init; }

    public FakeRecent Recent { get; }

    public List<string> Opened { get; } = [];

    public List<string> Located { get; } = [];

    public ProjectFacts Describe(RecentProject project)
    {
        if (Throws)
        {
            throw new InvalidOperationException("The app could not read this one.");
        }

        return Facts.TryGetValue(project.Path, out var facts) ? facts : new ProjectFacts();
    }

    public Task<ProjectChoice?> CreateAsync(Window owner) =>
        Task.FromResult<ProjectChoice?>(null);

    public Task<ProjectChoice?> BrowseAsync(Window owner, RecentProject? replacing)
    {
        if (replacing is { } lost)
        {
            Located.Add(lost.Name);
        }

        return Task.FromResult<ProjectChoice?>(null);
    }

    public Task<bool> OpenAsync(RecentProject project, bool inNewWindow, Window owner)
    {
        Opened.Add(project.Name);

        return Task.FromResult(true);
    }
}

/// <summary>The store, in memory. The real one is covered in Kitbash.Core.Tests.</summary>
internal sealed class FakeRecent : IRecentProjects
{
    private readonly List<RecentProject> _all;

    public FakeRecent(IEnumerable<RecentProject> seed)
    {
        _all = [.. seed];
    }

    public IReadOnlyList<RecentProject> All => _all;

    public RecentProject Remember(string path, string name)
    {
        var kept = _all.FirstOrDefault(entry => entry.Path == path);
        var entry = new RecentProject(path, name, DateTimeOffset.UtcNow, kept?.Exists ?? true);

        _all.RemoveAll(one => one.Path == path);
        _all.Insert(0, entry);

        return entry;
    }

    public void Rename(string path, string name)
    {
        var index = _all.FindIndex(entry => entry.Path == path);

        if (index >= 0)
        {
            _all[index] = _all[index] with { Name = name };
        }
    }

    public void Forget(string path) => _all.RemoveAll(entry => entry.Path == path);

    public void Refresh()
    {
    }
}
