using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A uniform spatial hash over the wires, the same shape as <see cref="GraphIndex"/>. It is
/// what keeps culling for a draw and hit testing under the pointer costing what is on screen
/// rather than what the graph holds.
///
/// A wire is reindexed only when one of its two nodes moves, so the churn a moving segment
/// would cause is bounded by what is being dragged.
/// </summary>
internal sealed class LinkIndex(double cell)
{
    private readonly Dictionary<long, List<GraphLink>> _cells = [];

    private int _stamp;

    public void Add(GraphLink link, Rect box)
    {
        var (x0, y0, x1, y1) = Span(box);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var key = Key(x, y);

                if (!_cells.TryGetValue(key, out var bucket))
                {
                    _cells[key] = bucket = [];
                }

                bucket.Add(link);
            }
        }
    }

    public void Remove(GraphLink link, Rect box)
    {
        var (x0, y0, x1, y1) = Span(box);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (_cells.TryGetValue(Key(x, y), out var bucket))
                {
                    bucket.Remove(link);
                }
            }
        }
    }

    /// <summary>Moves a wire, doing nothing at all when it stayed in the same cells.</summary>
    public void Update(GraphLink link, Rect was, Rect now)
    {
        var before = Span(was);
        var after = Span(now);

        if (before == after)
        {
            return;
        }

        Remove(link, was);
        Add(link, now);
    }

    public void Clear() => _cells.Clear();

    /// <summary>
    /// Every wire whose indexed box meets the area, appended to the list. The box is the one
    /// it was indexed with, which is never smaller than the wire, so a caller still tests
    /// what it is given.
    /// </summary>
    public void Query(Rect area, List<GraphLink> into)
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

                foreach (var link in bucket)
                {
                    if (link.QueryStamp == stamp)
                    {
                        continue;
                    }

                    link.QueryStamp = stamp;
                    into.Add(link);
                }
            }
        }
    }

    private (int X0, int Y0, int X1, int Y1) Span(Rect area) =>
        ((int)Math.Floor(area.X / cell),
            (int)Math.Floor(area.Y / cell),
            (int)Math.Floor(area.Right / cell),
            (int)Math.Floor(area.Bottom / cell));

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}
