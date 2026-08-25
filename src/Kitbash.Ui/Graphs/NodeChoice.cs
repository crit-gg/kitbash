namespace Kitbash.Ui.Controls;

/// <summary>One port a choice would give the node it makes.</summary>
public readonly record struct NodePortSpec(string Name, string Type, string? Value = null);

/// <summary>
/// One entry in the add node menu. It says enough to be listed, to be filtered against a pin
/// and to make the node, so an app usually writes one line per node kind it offers.
/// </summary>
public sealed class NodeChoice(string title, string kind = "", string group = "")
{
    public string Title { get; } = title;

    /// <summary>The family, which is what the icon and the colour come from.</summary>
    public string Kind { get; } = kind;

    /// <summary>The heading it is listed under. An empty group is listed with no heading.</summary>
    public string Group { get; } = group;

    public double Width { get; set; } = 176;

    public List<NodePortSpec> Inputs { get; } = [];

    public List<NodePortSpec> Outputs { get; } = [];

    /// <summary>
    /// Makes the node, when the ports alone are not enough to say what it is. The id is the
    /// graph's, so a node it makes is unique without the app counting anything.
    /// </summary>
    public Func<NodeChoice, string, GraphNode>? Build { get; set; }

    /// <summary>Anything the app wants to hang off this choice.</summary>
    public object? Tag { get; set; }

    /// <summary>
    /// What the entry is called by its type, which is its first output or, for a sink, its
    /// first input. The design draws it at the end of the row in the type's own colour.
    /// </summary>
    public string Type =>
        Outputs.Count > 0 ? Outputs[0].Type : Inputs.Count > 0 ? Inputs[0].Type : string.Empty;

    public NodeChoice With(string name, string type, string? value = null)
    {
        Inputs.Add(new NodePortSpec(name, type, value));
        return this;
    }

    public NodeChoice Gives(string name, string type)
    {
        Outputs.Add(new NodePortSpec(name, type));
        return this;
    }

    public NodeChoice Wide(double width)
    {
        Width = width;
        return this;
    }

    public GraphNode Make(string id)
    {
        if (Build is not null)
        {
            return Build(this, id);
        }

        var node = new GraphNode(id, Title, Kind, Width);

        foreach (var port in Inputs)
        {
            node.AddInput(port.Name, port.Type, port.Value);
        }

        foreach (var port in Outputs)
        {
            node.AddOutput(port.Name, port.Type);
        }

        return node;
    }

    /// <summary>Whether a wire off this pin could land on the node this choice makes.</summary>
    public bool Takes(GraphPort from, IPortRules rules)
    {
        if (from.Direction == PortDirection.Output)
        {
            foreach (var port in Inputs)
            {
                if (rules.CanJoin(from, new GraphPort(port.Name, port.Type, PortDirection.Input)))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var port in Outputs)
        {
            if (rules.CanJoin(new GraphPort(port.Name, port.Type, PortDirection.Output), from))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the typed query names this entry.</summary>
    public bool Matches(string query) =>
        query.Length == 0 ||
        Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        Group.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}

/// <summary>A heading in the add node menu. It is a row so the list can virtualise.</summary>
public sealed class NodeChoiceHeading(string group)
{
    public string Group { get; } = group;

    /// <summary>
    /// Capitalised here rather than by the app, since a group name is written once and read
    /// in this one place. Avalonia has no text transform, so it is done to the string.
    /// </summary>
    public string Text { get; } = group.ToUpperInvariant();
}

/// <summary>One offered entry, with its icon and colours already worked out.</summary>
public sealed class NodeChoiceRow(NodeChoice choice, IconGlyph? icon, Avalonia.Media.IBrush ink, Avalonia.Media.IBrush typeInk)
{
    public NodeChoice Choice { get; } = choice;

    public IconGlyph Icon { get; } = icon ?? IconGlyph.Cube;

    public bool HasIcon { get; } = icon is not null;

    public Avalonia.Media.IBrush Ink { get; } = ink;

    public Avalonia.Media.IBrush TypeInk { get; } = typeInk;

    public string Title => Choice.Title;

    public string Type => Choice.Type;
}
