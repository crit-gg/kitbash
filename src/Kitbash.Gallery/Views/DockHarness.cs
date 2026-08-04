using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Kitbash.Gallery.Views;

/// <summary>A document in the docking harness. The body is the only thing it carries.</summary>
public sealed class HarnessDocument : Document
{
    public string Body { get; init; } = string.Empty;
}

/// <summary>A tool in the docking harness.</summary>
public sealed class HarnessTool : Tool
{
    public string Body { get; init; } = string.Empty;
}

/// <summary>
/// The layout the gallery docks. A proportional split with a document dock, a tool dock
/// either side and one along the foot, which is the shape every part of the docking
/// theme has to be checked against.
/// </summary>
public sealed class DockHarness : Factory
{
    private IDockable? _first;

    /// <summary>The dockables the layout was built from, by id.</summary>
    private readonly Dictionary<string, IDockable> _built = [];

    public override IRootDock CreateLayout()
    {
        var overview = new HarnessDocument
        {
            Id = "Overview", Title = "Overview", Body = "Document content", IsModified = true
        };
        var details = new HarnessDocument { Id = "Details", Title = "Details", Body = "Document content" };
        var notes = new HarnessDocument { Id = "Notes", Title = "Notes", Body = "Document content", CanClose = true };

        var explorer = new HarnessTool { Id = "Explorer", Title = "Explorer", Body = "Tool content" };
        var history = new HarnessTool { Id = "History", Title = "History", Body = "Tool content" };
        var problems = new HarnessTool { Id = "Problems", Title = "Problems", Body = "Tool content" };
        var output = new HarnessTool { Id = "Output", Title = "Output", Body = "Tool content" };
        var inspector = new HarnessTool { Id = "Inspector", Title = "Inspector", Body = "Tool content" };

        var documents = new DocumentDock
        {
            Id = "Documents",
            IsCollapsable = false,
            ActiveDockable = overview,
            VisibleDockables = CreateList<IDockable>(overview, details, notes),
            CanCreateDocument = false
        };

        var middle = new ProportionalDock
        {
            Proportion = 0.72,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                documents,
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    Proportion = 0.3,
                    Alignment = Alignment.Bottom,
                    ActiveDockable = problems,
                    VisibleDockables = CreateList<IDockable>(problems, output)
                })
        };

        var layout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                new ToolDock
                {
                    Proportion = 0.2,
                    Alignment = Alignment.Left,
                    ActiveDockable = explorer,
                    VisibleDockables = CreateList<IDockable>(explorer, history)
                },
                new ProportionalDockSplitter(),
                middle,
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    Proportion = 0.18,
                    Alignment = Alignment.Right,
                    ActiveDockable = inspector,
                    VisibleDockables = CreateList<IDockable>(inspector)
                })
        };

        _first = overview;

        foreach (var dockable in new IDockable[]
                 {
                     overview, details, notes, explorer, history, problems, output, inspector, documents
                 })
        {
            _built[dockable.Id] = dockable;
        }

        var root = CreateRootDock();

        root.Id = "Root";
        root.IsCollapsable = false;
        root.ActiveDockable = layout;
        root.DefaultDockable = layout;
        root.VisibleDockables = CreateList<IDockable>(layout);
        root.LeftPinnedDockables = CreateList<IDockable>();
        root.RightPinnedDockables = CreateList<IDockable>();
        root.TopPinnedDockables = CreateList<IDockable>();
        root.BottomPinnedDockables = CreateList<IDockable>();

        return root;
    }

    /// <summary>One of the dockables the layout was built from, or null.</summary>
    public IDockable? Built(string id) => _built.GetValueOrDefault(id);

    /// <summary>
    /// Where a torn out dockable goes. Dock builds no window without this, so a tool that
    /// wants tearing out registers one the way the harness does.
    /// </summary>
    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }

    /// <summary>The layout, built and initialised, ready to hand to a DockControl.</summary>
    public IRootDock Build()
    {
        var layout = CreateLayout();

        InitLayout(layout);

        // A dock draws its open tab differently when it holds the focus, and nothing has
        // been clicked yet, so the harness says which one starts with it.
        if (_first is { } first)
        {
            SetFocusedDockable(layout, first);
        }

        return layout;
    }
}
