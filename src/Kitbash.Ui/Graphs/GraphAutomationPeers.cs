using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What the canvas says out loud. Avalonia ships no graph pattern, so as with the grids this
/// is a name and a help text and no more: the canvas says how big it is and what is picked,
/// and a card says what node it is holding.
/// </summary>
public class NodeGraphAutomationPeer(NodeGraph owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string GetClassNameCore() => nameof(NodeGraph);

    protected override string? GetNameCore() => base.GetNameCore() ?? "Node graph";

    protected override string? GetHelpTextCore()
    {
        if (owner.Model is not { } model)
        {
            return "Empty";
        }

        var picked = owner.Selection.Anchor?.Label;
        var counts = $"{Many(model.Nodes.Count, "node")}, {Many(model.Links.Count, "wire")}";

        return picked is null
            ? $"{counts}, nothing picked"
            : $"{counts}, {Many(owner.Selection.Items.Count, "item")} picked, {picked} last";
    }

    private static string Many(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";
}

/// <summary>One node, named by its title so a reader can tell one card from another.</summary>
public class NodeCardAutomationPeer(NodeCard owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => nameof(NodeCard);

    protected override string? GetNameCore() => owner.Node?.Title ?? base.GetNameCore();

    protected override string? GetHelpTextCore()
    {
        if (owner.Node is not { } node)
        {
            return null;
        }

        var says = new List<string>();

        if (node.Kind.Length > 0)
        {
            says.Add(node.Kind);
        }

        says.Add($"{node.Inputs.Count} in, {node.Outputs.Count} out");

        if (node.IsCollapsed)
        {
            says.Add("collapsed");
        }

        if (node.IsBypassed)
        {
            says.Add("bypassed");
        }

        if (node.HasProblem)
        {
            says.Add("has a problem");
        }

        return string.Join(", ", says);
    }
}
