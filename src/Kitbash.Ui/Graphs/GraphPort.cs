namespace Kitbash.Ui.Controls;

/// <summary>
/// One pin on a node. Its type is a name the app chooses and <see cref="IPortRules"/> reads,
/// so the library never knows what a Float is.
/// </summary>
public sealed class GraphPort
{
    public GraphPort(string name, string type, PortDirection direction)
    {
        Name = name;
        Type = type;
        Direction = direction;
    }

    public string Name { get; set; }

    /// <summary>The type name. Two ports may only be joined when the rules say so.</summary>
    public string Type { get; set; }

    public PortDirection Direction { get; }

    /// <summary>
    /// The value drawn in the row while nothing is wired into it. Null means the port has no
    /// value of its own, which is what the design draws the missing mark for.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Its place in its own direction's list. The model keeps it true.</summary>
    public int Index { get; internal set; }

    /// <summary>
    /// What the pin is drawn as. A second reading of the type beside its colour, which is
    /// what a person who cannot tell two colours apart has to go on.
    /// </summary>
    public PortShape Shape { get; set; }

    /// <summary>The node it belongs to. Set when it is added.</summary>
    public GraphNode? Node { get; internal set; }

    /// <summary>Anything the app wants to hang off this port.</summary>
    public object? Tag { get; set; }
}
