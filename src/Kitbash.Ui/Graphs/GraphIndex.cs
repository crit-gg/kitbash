using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A uniform spatial hash over everything on the canvas that has a box. What makes the
/// realise pass, the marquee and hit testing cost what is on screen rather than what is in
/// the graph.
/// </summary>
internal sealed class GraphIndex(double cell)
{
    private readonly Dictionary<long, List<GraphItem>> _cells = [];

    private double _cell = cell;
    private int _stamp;

    public void Add(GraphItem item)
    {
        foreach (var key in Keys(item.Bounds))
        {
            if (!_cells.TryGetValue(key, out var bucket))
            {
                _cells[key] = bucket = [];
            }

            bucket.Add(item);
        }
    }

    public void Remove(GraphItem item, Rect bounds)
    {
        foreach (var key in Keys(bounds))
        {
            if (_cells.TryGetValue(key, out var bucket))
            {
                bucket.Remove(item);
            }
        }
    }

    /// <summary>Moves an item, doing nothing at all when it stayed in the same cells.</summary>
    public void Update(GraphItem item, Rect was)
    {
        var (x0, y0, x1, y1) = Span(was);
        var (nx0, ny0, nx1, ny1) = Span(item.Bounds);

        if (x0 == nx0 && y0 == ny0 && x1 == nx1 && y1 == ny1)
        {
            return;
        }

        Remove(item, was);
        Add(item);
    }

    /// <summary>
    /// Emptied, at whatever cell size is in force now. The stamp counter is kept, since an
    /// item holds the stamp it was last returned under and starting over would make the next
    /// query take those items for ones it had already handed back.
    /// </summary>
    public void Clear(double cell)
    {
        _cells.Clear();
        _cell = cell;
    }

    /// <summary>
    /// Everything whose box meets the rectangle, appended to the list. An item spanning
    /// several cells is returned once, which the stamp is what settles.
    /// </summary>
    public void Query(Rect area, List<GraphItem> into)
    {
        var stamp = ++_stamp;
        var (x0, y0, x1, y1) = Span(area);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (!_cells.TryGetValue(Key(x, y), out var bucket))
                {
                    continue;
                }

                foreach (var item in bucket)
                {
                    if (item.QueryStamp == stamp || !item.Bounds.Intersects(area))
                    {
                        continue;
                    }

                    item.QueryStamp = stamp;
                    into.Add(item);
                }
            }
        }
    }

    private (int X0, int Y0, int X1, int Y1) Span(Rect area) =>
        ((int)Math.Floor(area.X / _cell),
            (int)Math.Floor(area.Y / _cell),
            (int)Math.Floor(area.Right / _cell),
            (int)Math.Floor(area.Bottom / _cell));

    private IEnumerable<long> Keys(Rect area)
    {
        var (x0, y0, x1, y1) = Span(area);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                yield return Key(x, y);
            }
        }
    }

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}
