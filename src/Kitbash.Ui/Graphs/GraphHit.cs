namespace Kitbash.Ui.Controls;

/// <summary>What is under a point on the canvas.</summary>
public enum GraphHitKind
{
    None,

    /// <summary>A node's body.</summary>
    Node,

    /// <summary>A node's title strip, which is what a drag on a collapsed node lands on.</summary>
    NodeHeader,

    /// <summary>The caret at the end of a node's header.</summary>
    NodeCaret,

    /// <summary>A pin.</summary>
    Port,

    /// <summary>A wire.</summary>
    Link,

    /// <summary>A wire's reroute handle.</summary>
    Reroute,

    /// <summary>A frame's body, which is behind everything and picks nothing on its own.</summary>
    Frame,

    /// <summary>A frame's label tab, which is what a frame is dragged by.</summary>
    FrameLabel,

    /// <summary>A frame's edge or corner, which is what a frame is resized by.</summary>
    FrameEdge,

    /// <summary>A note.</summary>
    Note,
}

/// <summary>
/// One answer from a hit test. The canvas tests the model rather than the visual tree,
/// because a captured pointer never reaches whatever a drag passes over.
/// </summary>
public readonly record struct GraphHit(
    GraphHitKind Kind,
    GraphItem? Item = null,
    GraphPort? Port = null,
    GraphLink? Link = null,
    GraphEdges Edge = GraphEdges.None,
    int Index = 0)
{
    public static GraphHit Nothing { get; } = new(GraphHitKind.None);

    public GraphNode? Node => Item as GraphNode;

    public bool IsNode => Kind is GraphHitKind.Node or GraphHitKind.NodeHeader or GraphHitKind.NodeCaret;

    public bool IsLink => Kind is GraphHitKind.Link;

    public GraphFrame? Frame => Item as GraphFrame;
}
