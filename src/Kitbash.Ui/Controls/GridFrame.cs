using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The chrome both grids share: resolving the columns against the viewport, keeping the
/// header over the body as it scrolls sideways, and telling realised rows to lay out again.
/// </summary>
internal sealed class GridFrame(ItemsControl owner, GridColumns columns)
{
    private DataGridHeader? header;
    private ScrollViewer? body;
    private ScrollBar? lane;
    private IDisposable? watchingViewport;
    private IDisposable? watchingExtent;
    private IDisposable? watchingOffset;
    private double viewport;

    /// <summary>Wires the parts a template just handed over, dropping whatever was held.</summary>
    public void Attach(DataGridHeader? next, ScrollViewer? scroller)
    {
        watchingViewport?.Dispose();
        watchingExtent?.Dispose();
        watchingOffset?.Dispose();
        watchingViewport = null;
        watchingExtent = null;
        watchingOffset = null;

        header = next;
        body = scroller;
        lane = null;

        if (header is not null)
        {
            header.Columns = columns;
        }

        if (scroller is null)
        {
            return;
        }

        // The viewport is the only honest width to lay columns out against, and it arrives
        // one pass after the first layout, so the first resolve settles rather than answers.
        watchingViewport = scroller.GetObservable(ScrollViewer.ViewportProperty)
            .Subscribe(new AnonymousObserver<Size>(size => Resolve(size.Width)));

        // The extent is what says whether the vertical bar is up, and the viewport does not
        // move when it appears, so without this the columns never hear about the lane.
        watchingExtent = scroller.GetObservable(ScrollViewer.ExtentProperty)
            .Subscribe(new AnonymousObserver<Size>(_ => Resolve()));

        watchingOffset = scroller.GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(offset => Sync(offset.X)));
    }

    /// <summary>Where the body is scrolled to. Nothing at all before a template has run.</summary>
    public Vector Offset
    {
        get => body?.Offset ?? default;
        set
        {
            if (body is { } scroller)
            {
                scroller.Offset = value;
            }
        }
    }

    /// <summary>Works the widths out again at the width already known.</summary>
    public void Resolve() => Resolve(viewport);

    /// <summary>
    /// Puts the body back to the first row, keeping the sideways offset. For a page turn,
    /// where staying halfway down means landing in the middle of the new page.
    /// </summary>
    public void ToTop() => Offset = Offset.WithY(0);

    /// <summary>The set of columns has changed, so the header and every row are made again.</summary>
    public void Rebuild()
    {
        header?.Rebuild();

        foreach (var container in owner.GetRealizedContainers())
        {
            (container as IGridRowLayout)?.Rebuild();
        }
    }

    private void Resolve(double available)
    {
        viewport = available;

        if (!columns.Resolve(Math.Max(0, available - Lane())))
        {
            return;
        }

        header?.Relayout();

        foreach (var container in owner.GetRealizedContainers())
        {
            (container as IGridRowLayout)?.Relayout();
        }
    }

    /// <summary>
    /// How much of the body the vertical scroll bar covers. Avalonia overlays a bar rather
    /// than giving it a column of its own, so columns that fill the viewport put the last
    /// one under the lane unless this comes off the width first.
    /// </summary>
    private double Lane()
    {
        lane ??= body?.GetVisualDescendants()
            .OfType<ScrollBar>()
            .FirstOrDefault(bar => bar.Orientation == Orientation.Vertical);

        return lane is { IsVisible: true } bar ? bar.Bounds.Width : 0;
    }

    private void Sync(double x)
    {
        if (header?.Scroller is { } strip)
        {
            strip.Offset = new Vector(x, 0);
        }
    }
}
