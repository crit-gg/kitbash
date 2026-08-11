using Avalonia.Interactivity;
using Dock.Model.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Tabs, and the dock harness under the Slate theme.</summary>
public partial class DockingPage : GalleryPage
{
    /// <summary>The docking harness. It owns the layout, so rebuilding is one call.</summary>
    private DockHarness? _dock;

    public DockingPage()
    {
        InitializeComponent();

        BuildDock();
    }

    private void BuildDock()
    {
        _dock = new DockHarness();
        Docking.Layout = _dock.Build();
    }

    private HarnessTool? Tool(string id) => _dock?.Built(id) as HarnessTool;

    private void OnFloatTool(object? sender, RoutedEventArgs e)
    {
        if (Tool("Inspector") is { } tool)
        {
            _dock?.FloatDockable(tool);
        }
    }

    private void OnPinTool(object? sender, RoutedEventArgs e)
    {
        if (Tool("Explorer") is { } tool)
        {
            _dock?.PinDockable(tool);
        }
    }

    /// <summary>Empties the document dock, which is the one state a dock draws by itself.</summary>
    private void OnCloseDocuments(object? sender, RoutedEventArgs e)
    {
        if (_dock?.Built("Documents") is not IDocumentDock { VisibleDockables: { } open })
        {
            return;
        }

        foreach (var document in open.ToList())
        {
            _dock.CloseDockable(document);
        }
    }

    private void OnResetDock(object? sender, RoutedEventArgs e) => BuildDock();
}
