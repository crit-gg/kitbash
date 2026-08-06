using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kitbash.Core.Projects;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Projects;

namespace Kitbash.Gallery.Views;

/// <summary>
/// The design's three hosts, so one window can be read as each of them. Foundry lists
/// Godot projects, Hoard lists asset libraries and Splice lists repositories.
/// </summary>
public enum ProjectHost
{
    Foundry,
    Hoard,
    Splice,
}

/// <summary>
/// A recent list held in memory. The gallery draws the library and writes nothing, so it
/// never touches the state file a real app keeps its list in.
/// </summary>
public sealed class HarnessProjects : IRecentProjects
{
    private readonly List<RecentProject> _all;

    public HarnessProjects(IEnumerable<RecentProject> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        _all = [.. seed];
    }

    public IReadOnlyList<RecentProject> All => _all;

    public RecentProject Remember(string path, string name)
    {
        var kept = _all.FirstOrDefault(entry =>
            string.Equals(entry.Path, path, StringComparison.Ordinal));

        var entry = new RecentProject(path, name, DateTimeOffset.UtcNow, kept?.Exists ?? true);

        _all.RemoveAll(one => string.Equals(one.Path, path, StringComparison.Ordinal));
        _all.Insert(0, entry);

        return entry;
    }

    public void Rename(string path, string name)
    {
        var index = _all.FindIndex(entry =>
            string.Equals(entry.Path, path, StringComparison.Ordinal));

        if (index >= 0)
        {
            _all[index] = _all[index] with { Name = name };
        }
    }

    public void Forget(string path) =>
        _all.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.Ordinal));

    public void Refresh()
    {
    }
}

/// <summary>
/// One host's answer to what a project is. Everything here is what a real app would
/// decide, which is the whole point of the seam.
/// </summary>
public sealed class HarnessProjectKind : IProjectKind
{
    private readonly Dictionary<string, ProjectFacts> _facts;

    private HarnessProjectKind(ProjectWords words, Dictionary<string, ProjectFacts> facts)
    {
        Words = words;
        _facts = facts;
    }

    public ProjectWords Words { get; }

    /// <summary>The gallery has nowhere to open one, so its window stays put.</summary>
    public bool ClosesOnOpen => false;

    public IReadOnlyList<ProjectPage> Pages { get; private init; } = [];

    public IRecentProjects Recent { get; private init; } = new HarnessProjects([]);

    /// <summary>What the last gesture asked for, drawn back on the gallery page.</summary>
    public Action<string>? Reported { get; set; }

    public ProjectFacts Describe(RecentProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return _facts.TryGetValue(project.Path, out var facts) ? facts : new ProjectFacts();
    }

    public Task<ProjectChoice?> CreateAsync(Window owner)
    {
        Reported?.Invoke($"{Words.NewLabel} was asked for");

        return Task.FromResult<ProjectChoice?>(null);
    }

    public Task<ProjectChoice?> BrowseAsync(Window owner, RecentProject? replacing)
    {
        Reported?.Invoke(replacing is null
            ? "Browsing for one"
            : $"Locating {replacing.Name}");

        return Task.FromResult<ProjectChoice?>(null);
    }

    public Task<bool> OpenAsync(RecentProject project, bool inNewWindow, Window owner)
    {
        ArgumentNullException.ThrowIfNull(project);

        Reported?.Invoke(inNewWindow
            ? $"Opening {project.Name} in a new window"
            : $"Opening {project.Name}");

        return Task.FromResult(true);
    }

    /// <summary>The design's own rows, times and chips, one host at a time.</summary>
    public static HarnessProjectKind For(ProjectHost host) => host switch
    {
        ProjectHost.Hoard => Build(
            new ProjectWords("Hoard", "library", "libraries")
            {
                Version = "1.2.4",
                NewLabel = "New library",
            },
            [
                Row("D:/dev/slopworks/art", "Slopworks art", 11, "4812 ASSETS", BadgeTier.Neutral),
                Row("//studio/shared/vfx", "Shared VFX", 120, "NETWORK", BadgeTier.Graph),
                Row("D:/dev/slopworks/audio", "Audio pass 3", 1440, "716 ASSETS", BadgeTier.Neutral),
                Row("E:/exports/marketing", "Marketing renders", 10080, "READ ONLY",
                    BadgeTier.Modified, IconGlyph.InfoCircle),
                Row("E:/archive/ashfall-art", "Ashfall art dump", 99000, "OFFLINE DRIVE",
                    BadgeTier.Error, IconGlyph.AlertCircle, broken: true),
            ]),

        // The one host here with a page of its own, so the rail is exercised both ways.
        ProjectHost.Splice => Build(
            new ProjectWords("Splice", "repository", "repositories")
            {
                Version = "0.6.1",
                NewLabel = "Clone a repository",
            },
            [
                Row("D:/dev/slopworks/godot", "slopworks", 0, "1 CONFLICT",
                    BadgeTier.Error, IconGlyph.AlertCircle),
                Row("D:/dev/slopworks-tools", "slopworks-tools", 34, "2 AHEAD", BadgeTier.Graph),
                Row("D:/dev/godot-fork", "engine-fork", 2880, "CLEAN", BadgeTier.Accent),
                Row("C:/Users/you/jam2026", "jam-entry-2026", 20000, "CLEAN", BadgeTier.Accent),
            ],
            [
                new ProjectPage(IconGlyph.GitBranch, "Remotes", Note(
                    "A page the app added. The window draws the rail item and hands the "
                    + "pane over, and knows nothing else about what is in here. The page "
                    + "is built once and kept, so leaving it and coming back finds it as "
                    + "it was.")),
            ]),

        _ => Build(
            new ProjectWords("Foundry", "project", "projects") { Version = "0.10.0" },
            [
                Row("D:/dev/slopworks/godot", "Slopworks", 2, "GODOT 4.7.1", BadgeTier.Accent),
                Row("D:/dev/slopworks-demo/godot", "Slopworks demo build", 1440, "READ ONLY",
                    BadgeTier.Modified, IconGlyph.InfoCircle),
                Row("D:/dev/spikes/heat-rebalance", "Heat rebalance spike", 4320,
                    "GODOT 4.8 BETA", BadgeTier.Graph),
                Row("C:/Users/you/Documents/wb-sandbox", "Tinkering sandbox", 10080,
                    "DATA ONLY", BadgeTier.Neutral),
                Row("D:/dev/ashfall", "Ashfall prototype", 74000, "NOT FOUND",
                    BadgeTier.Error, IconGlyph.AlertCircle, broken: true),
                Row("D:/dev/scratch/belt-sim", "Belt sim scratch", 91000, "GODOT 4.7",
                    BadgeTier.Accent),
            ]),
    };

    private static HarnessProjectKind Build(
        ProjectWords words,
        IReadOnlyList<Seed> rows,
        IReadOnlyList<ProjectPage>? pages = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new HarnessProjectKind(
            words,
            rows.ToDictionary(row => row.Path, row => row.Facts, StringComparer.Ordinal))
        {
            Pages = pages ?? [],
            Recent = new HarnessProjects(rows.Select(row => new RecentProject(
                row.Path,
                row.Name,
                now.AddMinutes(-row.Minutes),
                !row.Facts.IsBroken))),
        };
    }

    /// <summary>Stands in for whatever an app would really put on a page of its own.</summary>
    private static Control Note(string text) => new TextBlock
    {
        Text = text,
        Margin = new Thickness(24),
        MaxWidth = 360,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Seed Row(
        string path,
        string name,
        int minutes,
        string chip,
        BadgeTier tier,
        IconGlyph? glyph = null,
        bool broken = false) =>
        new(path, name, minutes, new ProjectFacts
        {
            Chip = chip,
            ChipTier = tier,
            ChipGlyph = glyph,
            IsBroken = broken,
        });

    private sealed record Seed(string Path, string Name, int Minutes, ProjectFacts Facts);
}
