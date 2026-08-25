namespace Kitbash.Ui.Controls;

/// <summary>
/// Whether two ports may be joined. The library knows nothing about types, so this is where
/// an app says that an Int reaches a Float and a Bool does not.
/// </summary>
public interface IPortRules
{
    /// <summary>Whether a wire may run from this output to this input.</summary>
    bool CanJoin(GraphPort from, GraphPort to);
}

/// <summary>
/// The default rule: equal type names join and nothing else does. It is deliberately strict,
/// since a graph that silently widens is a graph whose author cannot see why.
/// </summary>
public sealed class PortRules : IPortRules
{
    public static PortRules Strict { get; } = new();

    public bool CanJoin(GraphPort from, GraphPort to) =>
        string.Equals(from.Type, to.Type, StringComparison.Ordinal);
}

/// <summary>A rule built from a delegate, for an app that has one line to say.</summary>
public sealed class DelegatePortRules(Func<GraphPort, GraphPort, bool> rule) : IPortRules
{
    public bool CanJoin(GraphPort from, GraphPort to) => rule(from, to);
}
