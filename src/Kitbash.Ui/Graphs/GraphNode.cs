using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One node. Its box comes from its port count and the metrics, so the graph can place it,
/// cull it and route its wires without any control being realised for it.
/// </summary>
public sealed class GraphNode : GraphItem
{
    private readonly List<GraphPort> _inputs = [];
    private readonly List<GraphPort> _outputs = [];

    private string _title;
    private string _kind;
    private bool _isCollapsed;
    private bool _isBypassed;
    private bool _hasProblem;
    private string? _badge;
    private double? _bodyHeight;
    private double _footerHeight;
    private PortLayout _portLayout;

    public GraphNode(string id, string title, string kind = "", double width = 176)
        : base(id)
    {
        _title = title;
        _kind = kind;
        Width = width;
        Refresh();
    }

    public string Title
    {
        get => _title;
        set => SetLook(ref _title, value);
    }

    /// <summary>A node's label is its title.</summary>
    public override string? Label
    {
        get => _title;
        set => SetLook(ref _title, value ?? string.Empty);
    }

    /// <summary>
    /// What family of node this is. A name the app maps to an icon and a colour through
    /// <see cref="NodeGraph.Kinds"/>, so the library never knows what a Math node is.
    /// </summary>
    public string Kind
    {
        get => _kind;
        set => SetLook(ref _kind, value);
    }

    /// <summary>Rolled up to its header, with one pin a side standing in for all of them.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetLook(ref _isCollapsed, value))
            {
                Refresh();
            }
        }
    }

    /// <summary>Switched off. The graph reads as though it were not here.</summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set => SetLook(ref _isBypassed, value);
    }

    /// <summary>Marked wrong by whatever reads the graph. The library never sets it.</summary>
    public bool HasProblem
    {
        get => _hasProblem;
        set => SetLook(ref _hasProblem, value);
    }

    /// <summary>The short word at the end of the header, or none.</summary>
    public string? Badge
    {
        get => _badge;
        set => SetLook(ref _badge, value);
    }

    /// <summary>
    /// The body's height when the node draws something other than port rows, such as a
    /// preview. Null means the rows decide it.
    /// </summary>
    public double? BodyHeight
    {
        get => _bodyHeight;
        set
        {
            if (SetLook(ref _bodyHeight, value))
            {
                Refresh();
                Reshaped();
            }
        }
    }

    /// <summary>A strip under the body, or zero for none.</summary>
    public double FooterHeight
    {
        get => _footerHeight;
        set
        {
            if (SetLook(ref _footerHeight, value))
            {
                Refresh();
            }
        }
    }

    /// <summary>
    /// Where the pins go. A node whose body it does not draw is always on the edge, whatever
    /// this says: rows line pins up with rows, and a node filled by a template has none.
    /// </summary>
    public PortLayout PortLayout
    {
        get => _bodyHeight is null ? _portLayout : PortLayout.Edge;
        set
        {
            if (SetLook(ref _portLayout, value))
            {
                Reshaped();
            }
        }
    }

    public IReadOnlyList<GraphPort> Inputs => _inputs;

    public IReadOnlyList<GraphPort> Outputs => _outputs;

    public override int Layer => 2;

    /// <summary>The header, which is where the title is drawn and where it is edited.</summary>
    public override Rect LabelBox(GraphMetrics metrics) =>
        new(X, Y, Width, metrics.HeaderHeight);

    public GraphPort Add(GraphPort port)
    {
        var list = port.Direction == PortDirection.Input ? _inputs : _outputs;

        port.Index = list.Count;
        port.Node = this;
        list.Add(port);

        Refresh();
        return port;
    }

    /// <summary>Adds an input in one line, which is what a catalogue entry builds with.</summary>
    public GraphPort AddInput(string name, string type, string? value = null) =>
        Add(new GraphPort(name, type, PortDirection.Input) { Value = value });

    public GraphPort AddOutput(string name, string type) =>
        Add(new GraphPort(name, type, PortDirection.Output));

    public int PortCount(PortDirection direction) =>
        direction == PortDirection.Input ? _inputs.Count : _outputs.Count;

    public GraphPort? Port(PortDirection direction, int index)
    {
        var list = direction == PortDirection.Input ? _inputs : _outputs;

        return index >= 0 && index < list.Count ? list[index] : null;
    }

    /// <summary>Where a port's pin sits, in graph units.</summary>
    public Point PortPoint(GraphPort port)
    {
        var offset = Metrics.PortOffset(this, port);

        return new Point(X + offset.X, Y + offset.Y);
    }

    /// <summary>
    /// The pin a collapsed node offers for a whole side, which is its first port. A
    /// collapsed node with no port on that side offers nothing.
    /// </summary>
    public GraphPort? CollapsedPort(PortDirection direction) => Port(direction, 0);

    internal override void Refresh() => Height = Measure();

    private double Measure()
    {
        var metrics = Metrics;

        if (_isCollapsed)
        {
            return metrics.HeaderHeight;
        }

        if (_bodyHeight is { } body)
        {
            return metrics.HeaderHeight + body + _footerHeight;
        }

        var rows = Math.Max(_inputs.Count, _outputs.Count);

        return metrics.HeaderHeight + metrics.BodyPadding * 2 + rows * metrics.RowHeight + _footerHeight;
    }
}
