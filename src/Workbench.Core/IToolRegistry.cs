namespace Workbench.Core;

/// <summary>
/// Source of the tools the launcher offers. Registration is deliberately explicit
/// rather than assembly scanned so that adding a tool is a visible code change.
/// </summary>
public interface IToolRegistry
{
    IReadOnlyList<ITool> Tools { get; }
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly List<ITool> _tools = [];

    public IReadOnlyList<ITool> Tools => _tools;

    public ToolRegistry Add(ITool tool)
    {
        _tools.Add(tool);
        return this;
    }
}
