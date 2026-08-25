using Avalonia;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A wire from an output to an input. It holds its own routed path and its own box, both
/// built once and thrown away only when one of its two nodes moves, so a pan or a zoom
/// rebuilds nothing.
/// </summary>
public sealed class GraphLink
{
    private readonly List<Point> _reroutes = [];

    public GraphLink(GraphPort from, GraphPort to)
    {
        if (from.Direction != PortDirection.Output || to.Direction != PortDirection.Input)
        {
            throw new ArgumentException("A link runs from an output to an input.");
        }

        From = from;
        To = to;
    }

    public GraphPort From { get; }

    public GraphPort To { get; }

    public GraphNode FromNode => From.Node!;

    public GraphNode ToNode => To.Node!;

    /// <summary>
    /// The points the wire is dragged through, in the order it passes them. A wire takes as
    /// many as a person adds, so a long one can be routed round whatever is in its way.
    /// </summary>
    public IReadOnlyList<Point> Reroutes => _reroutes;

    /// <summary>Adds a point at a place in the run, which is the segment it was added on.</summary>
    public void Reroute(int index, Point at)
    {
        _reroutes.Insert(Math.Clamp(index, 0, _reroutes.Count), at);
        Moved();
    }

    /// <summary>Adds a point at the end of the run.</summary>
    public void Reroute(Point at) => Reroute(_reroutes.Count, at);

    public void MoveReroute(int index, Point at)
    {
        if (index < 0 || index >= _reroutes.Count || _reroutes[index] == at)
        {
            return;
        }

        _reroutes[index] = at;
        Moved();
    }

    public void DropReroute(int index)
    {
        if (index < 0 || index >= _reroutes.Count)
        {
            return;
        }

        _reroutes.RemoveAt(index);
        Moved();
    }

    /// <summary>Puts a whole run back, which is what taking a carried wire back needs.</summary>
    public void SetReroutes(IReadOnlyList<Point> through)
    {
        _reroutes.Clear();
        _reroutes.AddRange(through);
        Moved();
    }

    public bool IsSelected { get; set; }

    /// <summary>Anything the app wants to hang off this link.</summary>
    public object? Tag { get; set; }

    /// <summary>The model holding it, or null while it is loose.</summary>
    internal GraphModel? Owner { get; set; }

    internal Geometry? Path { get; private set; }

    internal Rect PathBounds { get; private set; }

    internal Point FromPoint { get; private set; }

    internal Point ToPoint { get; private set; }

    /// <summary>The box it currently sits under in the index. Never smaller than the wire.</summary>
    internal Rect Indexed { get; set; }

    /// <summary>Which query last returned it, so one spanning several cells is returned once.</summary>
    internal int QueryStamp { get; set; }

    /// <summary>
    /// The whole run the wire passes through, from its output pin to its input pin. What
    /// routing walks and what a hit test asks which segment it landed on.
    /// </summary>
    internal IEnumerable<Point> Run()
    {
        yield return FromNode.PortPoint(From);

        foreach (var through in _reroutes)
        {
            yield return through;
        }

        yield return ToNode.PortPoint(To);
    }

    private void Moved()
    {
        Invalidate();
        Owner?.LinkMoved(this);
    }

    /// <summary>Throws the routed path away. The next draw builds it again.</summary>
    internal void Invalidate()
    {
        Path = null;
        Owner?.Invalidate();
    }

    /// <summary>
    /// The box the wire cannot leave. It is the routed path's own box when there is one and
    /// the rough one otherwise, so a pass over every link in a large graph reads a field
    /// rather than working two pin positions out again.
    /// </summary>
    internal Rect Box(GraphMetrics metrics) => Path is not null ? PathBounds : Rough(metrics);

    /// <summary>
    /// The box the wire cannot leave, from its two ends alone. Culling reads this rather
    /// than the path, so a wire off screen is never routed at all.
    /// </summary>
    internal Rect Rough(GraphMetrics metrics) => GraphBox.Around(Run()).Inflate(metrics.WireBow);

    /// <summary>
    /// The routed path, built on demand. The bow padding is what keeps the box honest for
    /// culling, since a cubic leaves the rectangle its two ends make.
    /// </summary>
    internal Geometry Route(WireStyle style, GraphMetrics metrics)
    {
        if (Path is not null)
        {
            return Path;
        }

        FromPoint = FromNode.PortPoint(From);
        ToPoint = ToNode.PortPoint(To);

        Path = WireRouter.Build(FromPoint, ToPoint, _reroutes, style, metrics);
        PathBounds = Path.Bounds.Inflate(metrics.WireWidthActive);

        return Path;
    }
}
