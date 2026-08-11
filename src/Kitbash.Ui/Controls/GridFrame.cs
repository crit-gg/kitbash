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
            header.Fitting += (_, column) => Fit(column);
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
    /// Sets a column to the widest thing in it. Only the rows that are realised are measured,
    /// since a virtualised grid has nothing else to measure, so it fits what is on screen
    /// rather than what is in the source.
    /// </summary>
    public void Fit(GridColumn column)
    {
        var widest = 0d;

        foreach (var container in owner.GetRealizedContainers())
        {
            if (container is not IGridRowLayout row)
            {
                continue;
            }

            foreach (var cell in row.Cells)
            {
                if (ReferenceEquals(cell.Column, column))
                {
                    widest = Math.Max(widest, Widest(cell));
                }
            }
        }

        if (header?.CellFor(column) is { } title)
        {
            widest = Math.Max(widest, Widest(title));
        }

        if (widest <= 0)
        {
            return;
        }

        columns.SetWidth(column, widest);
        Resolve();
    }

    /// <summary>How wide something wants to be with no limit on it.</summary>
    private static double Widest(Layoutable part)
    {
        part.Measure(new Size(double.PositiveInfinity, part.Bounds.Height));

        var wanted = part.DesiredSize.Width;

        // Measured again at the size it is really given, so the next layout pass is not
        // working from a constraint that was only ever asked as a question.
        part.Measure(part.Bounds.Size);

        return wanted;
    }

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

        Sync(body?.Offset.X ?? 0);
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

    /// <summary>
    /// The body has scrolled sideways. The header follows it, and every realised row is told
    /// how far, so a pinned column can be arranged that much further along and stay put.
    /// </summary>
    private void Sync(double x)
    {
        if (header?.Scroller is { } strip)
        {
            strip.Offset = new Vector(x, 0);
        }

        if (columns.PinnedWidth <= 0)
        {
            return;
        }

        header?.SetPinOffset(x);

        foreach (var container in owner.GetRealizedContainers())
        {
            (container as IGridRowLayout)?.SetPinOffset(x);
        }

        Seam?.Invoke(this, x > 0);
    }

    /// <summary>Raised with whether anything has scrolled under the pinned columns yet.</summary>
    public event EventHandler<bool>? Seam;
}
