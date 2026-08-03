using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;

namespace Workbench.Ui.Controls;

/// <summary>
/// The chrome both grids share: resolving the columns against the viewport, keeping the
/// header over the body as it scrolls sideways, and telling realised rows to lay out again.
/// </summary>
internal sealed class GridFrame(ItemsControl owner, GridColumns columns)
{
    private DataGridHeader? header;
    private IDisposable? watchingViewport;
    private IDisposable? watchingOffset;
    private double viewport;

    /// <summary>Wires the parts a template just handed over, dropping whatever was held.</summary>
    public void Attach(DataGridHeader? next, ScrollViewer? scroller)
    {
        watchingViewport?.Dispose();
        watchingOffset?.Dispose();
        watchingViewport = null;
        watchingOffset = null;

        header = next;

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

        watchingOffset = scroller.GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(offset => Sync(offset.X)));
    }

    /// <summary>Works the widths out again at the width already known.</summary>
    public void Resolve() => Resolve(viewport);

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

        if (!columns.Resolve(available))
        {
            return;
        }

        header?.Relayout();

        foreach (var container in owner.GetRealizedContainers())
        {
            (container as IGridRowLayout)?.Relayout();
        }
    }

    private void Sync(double x)
    {
        if (header?.Scroller is { } strip)
        {
            strip.Offset = new Vector(x, 0);
        }
    }
}
